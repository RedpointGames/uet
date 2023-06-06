namespace Redpoint.UET.Automation.Runner
{
    using Microsoft.Extensions.Logging;
    using Redpoint.UET.Automation.Model;
    using Redpoint.UET.Automation.TestLogging;
    using Redpoint.UET.Automation.TestNotification;
    using Redpoint.UET.Automation.TestReporter;
    using Redpoint.UET.Automation.Worker;
    using Redpoint.Unreal.Serialization;
    using Redpoint.Unreal.TcpMessaging;
    using Redpoint.Unreal.TcpMessaging.MessageTypes;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultAutomationRunner : IAutomationRunner
    {
        private readonly ILogger<DefaultAutomationRunner> _logger;
        private readonly IWorkerPoolFactory _workerPoolFactory;
        private readonly ITestLogger _testLogger;
        private readonly ITestNotification _notification;
        private readonly ITestReporter _reporter;
        private readonly DesiredWorkerDescriptor[] _workerDescriptors;
        private readonly string _testPrefix;
        private readonly TimeSpan? _timeout;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ConcurrentDictionary<DesiredWorkerDescriptor, WorkerGroupState> _tests;
        private readonly Dictionary<IWorker, WorkerState> _workers;
        private readonly Task _timeoutTask;
        private readonly Task _runTask;
        private IWorkerPool? _workerPool;

        private class WorkerState
        {
            public required TcpMessageTransportConnection TransportConnection { get; set; }

            public TestResult? CurrentTest { get; set; }

            public Guid NegotiatedSessionId { get; set; }

            public int NegotiatedEngineVersion { get; set; }

            public string? NegotiatedBuildDate { get; set; }

            public string? NegotiatedSessionOwner { get; set; }

            public MessageAddress? NegotiatedTargetAddress { get; set; }

            public Task? TestingTask { get; set; }

            public required IAsyncDisposable Handle { get; set; }
        }

        private class WorkerGroupState
        {
            public required DesiredWorkerDescriptor Descriptor { get; set; }

            public IReadOnlyList<TestResult> AllTests { get; set; } = new List<TestResult>();

            public ConcurrentQueue<TestResult> QueuedTests { get; } = new ConcurrentQueue<TestResult>();

            public ConcurrentBag<TestResult> ProcessedTests { get; } = new ConcurrentBag<TestResult>();

            public int RemainingTests { get; set; }

            public ManualResetEventSlim ReadyForTesting { get; } = new ManualResetEventSlim();
        }

        public DefaultAutomationRunner(
            ILogger<DefaultAutomationRunner> logger,
            IWorkerPoolFactory workerPoolFactory,
            ITestLogger testLogger,
            ITestNotification notification,
            ITestReporter reporter,
            IEnumerable<DesiredWorkerDescriptor> workerGroups,
            string testPrefix,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            _logger = logger;
            _workerPoolFactory = workerPoolFactory;
            _testLogger = testLogger;
            _notification = notification;
            _reporter = reporter;
            _workerDescriptors = workerGroups.ToArray();
            _testPrefix = testPrefix;
            _timeout = timeout;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _tests = new ConcurrentDictionary<DesiredWorkerDescriptor, WorkerGroupState>();
            _workers = new Dictionary<IWorker, WorkerState>();
            _timeoutTask = Task.Run(TimeoutAsync);
            _runTask = Task.Run(RunAsync);
        }

        private async Task TimeoutAsync()
        {
            if (_timeout.HasValue)
            {
                await Task.Delay((int)_timeout.Value.TotalMilliseconds, _cancellationTokenSource.Token);
                _cancellationTokenSource.Cancel();
            }
        }

        private async Task WorkerAddedToPoolAsync(IWorker worker)
        {
            _logger.LogTrace($"Received new worker {worker.Id} to pool");

            // Connect to the TCP socket.
            TcpClient? tcpClient = null;
            do
            {
                _logger.LogTrace($"Attempting to connect to worker {worker.Id} on endpoint {worker.EndPoint}");
                tcpClient = new TcpClient();
                var connectionCts = new CancellationTokenSource();
                var connectionTask = tcpClient.ConnectAsync(worker.EndPoint, connectionCts.Token).AsTask();
                await Task.WhenAny(connectionTask, Task.Delay(1000));
                if (!tcpClient.Connected)
                {
                    _logger.LogTrace($"Failed to connect to worker {worker.Id} on endpoint {worker.EndPoint} with 1 seconds, retrying");
                    connectionCts.Cancel();
                    continue;
                }
                break;
            } while (!_cancellationTokenSource.IsCancellationRequested);
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            _logger.LogTrace($"Successfully connected to worker {worker.Id} on endpoint {worker.EndPoint}");

            // Connect to the worker and add it.
            _logger.LogTrace($"Setting up Unreal TCP transport connection...");

            var workerState = new WorkerState
            {
                Handle = await _workerPool!.ReserveAsync(worker),
                TransportConnection = await TcpMessageTransportConnection.CreateAsync(tcpClient, _logger),
                CurrentTest = null,
            };
            _workers.Add(worker, workerState);
            var connection = workerState.TransportConnection;

            _logger.LogTrace($"Connected to worker {worker.Id}");

            try
            {
                // Negotiate with the worker connection so we can start sending messages to it.
                _logger.LogTrace($"Starting negotiation with worker {worker.Id}");
                await NegotiateWithWorkerAsync(worker);

                // If this is the first worker for this descriptor, get the list of tests from it.
                var groupState = new WorkerGroupState
                {
                    Descriptor = worker.Descriptor
                };
                if (_tests.TryAdd(worker.Descriptor, groupState))
                {
                    _logger.LogTrace($"Detected that we need to use worker {worker.Id} to discover tests...");

                    try
                    {
                        // Discover the tests.
                        var discoveredTests = new AutomationWorkerRequestTestsReplyComplete();
                        connection.Send(workerState.NegotiatedTargetAddress!, new AutomationWorkerRequestTests()
                        {
                            DeveloperDirectoryIncluded = true,
                            RequestedTestFlags = worker.Descriptor.IsEditor
                                ? (AutomationTestFlags.EditorContext | AutomationTestFlags.ProductFilter)
                                : (AutomationTestFlags.ClientContext | AutomationTestFlags.ProductFilter),
                        });
                        _logger.LogTrace($"Waiting to get listed tests...");
                        await connection.ReceiveUntilAsync(message =>
                        {
                            switch (message.GetMessageData())
                            {
                                case AutomationWorkerRequestTestsReplyComplete response:
                                    discoveredTests = response;
                                    return Task.FromResult(true);
                            }

                            return Task.FromResult(false);
                        }, CancellationToken.None);
                        _logger.LogTrace($"Received {discoveredTests.Tests.Count} tests");

                        // Generate all of the pending tests.
                        groupState.AllTests = discoveredTests.Tests
                            .Where(x => x.FullTestPath.StartsWith(_testPrefix))
                            .Select(x => new TestResult
                            {
                                Platform = worker.Descriptor.Platform,
                                TestName = x.TestName,
                                FullTestPath = x.FullTestPath,
                                WorkerDisplayName = null,
                                TestStatus = TestResultStatus.NotRun,
                                DateStarted = DateTimeOffset.MinValue,
                                DateFinished = DateTimeOffset.MaxValue,
                                Duration = TimeSpan.Zero,
                                Entries = new TestResultEntry[0],
                            }).ToList();
                        foreach (var testToProcess in groupState.AllTests)
                        {
                            _testLogger.LogDiscovered(worker, testToProcess);
                            _notification.TestDiscovered(testToProcess);
                            groupState.RemainingTests++;
                            groupState.QueuedTests.Enqueue(testToProcess);
                        }

                        // We are now ready to process tests.
                        _logger.LogTrace($"Ready to begin testing for descriptor {groupState.Descriptor.Platform}");
                        groupState.ReadyForTesting.Set();
                    }
                    catch (Exception ex)
                    {
                        // We failed to list the tests. Remove the entry so another
                        // worker can pick up this worker, then throw.
                        _logger.LogError(ex, $"Failed to list tests from worker: {ex.Message}");
                        _tests.Remove(worker.Descriptor, out _);
                        throw;
                    }
                }

                // Run the task for this worker, which pulls tests off the queue, runs them and stores the results.
                _logger.LogTrace($"Starting testing task for worker {worker.Id}");
                workerState.TestingTask = Task.Run(async () =>
                {
                    await RunWorkerAsync(worker);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected exception during worker startup: {ex.Message}");
                await workerState.Handle.DisposeAsync();
                throw;
            }
        }

        private async Task RunWorkerAsync(IWorker worker)
        {
            var workerGroupState = _tests[worker.Descriptor];
            var workerState = _workers[worker];

            try
            {
                _logger.LogTrace($"Waiting for testing to be ready...");
                if (!workerGroupState.ReadyForTesting.Wait(0))
                {
                    // Wait until we can pull tests off the queue.
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }

                _logger.LogTrace($"Testing loop for worker {worker.Id} is now starting...");

                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    if (!workerGroupState.QueuedTests.TryDequeue(out var nextTest))
                    {
                        // We've finished running tests for this test group.
                        _logger.LogTrace($"We have finished running all tests for {worker.Id}.");
                        _workerPool!.FinishedWithWorker(worker);
                        if (workerGroupState.RemainingTests <= 0)
                        {
                            _workerPool.FinishedWithDescriptor(worker.Descriptor);
                        }
                        break;
                    }

                    // Process the next test.
                    _logger.LogTrace($"Pulled test {nextTest.FullTestPath} off the queue as worker {worker.Id}...");
                    nextTest.WorkerDisplayName = worker.DisplayName;
                    nextTest.DateStarted = DateTimeOffset.UtcNow;
                    nextTest.TestStatus = TestResultStatus.InProgress;
                    workerState.CurrentTest = nextTest;
                    _testLogger.LogStarted(worker, nextTest);
                    _notification.TestStarted(nextTest);
                    try
                    {
                        workerState.TransportConnection.Send(
                            workerState.NegotiatedTargetAddress!,
                            new AutomationWorkerRunTests
                            {
                                ExecutionCount = 1,
                                TestName = nextTest.TestName,
                                FullTestPath = nextTest.FullTestPath,
                                BeautifiedTestName = nextTest.FullTestPath,
                                bSendAnalytics = false,
                                RoleIndex = 0,
                            });
                        await workerState.TransportConnection.ReceiveUntilAsync(message =>
                        {
                            var data = message.GetMessageData();
                            var reply = data as AutomationWorkerRunTestsReply;
                            if (reply == null)
                            {
                                return Task.FromResult(false);
                            }

                            if (reply.TestName != nextTest.TestName)
                            {
                                return Task.FromResult(false);
                            }

                            var isDone = false;
                            switch (reply.State)
                            {
                                case AutomationState.Skipped:
                                    nextTest.TestStatus = TestResultStatus.Skipped;
                                    isDone = true;
                                    break;
                                case AutomationState.Success:
                                    nextTest.TestStatus = TestResultStatus.Passed;
                                    isDone = true;
                                    break;
                                case AutomationState.Fail:
                                    nextTest.TestStatus = TestResultStatus.Failed;
                                    isDone = true;
                                    break;
                            }

                            if (isDone)
                            {
                                nextTest.DateFinished = DateTimeOffset.UtcNow;
                                nextTest.Duration = TimeSpan.FromSeconds(reply.Duration);
                                nextTest.Entries = reply.Entries.Select(x =>
                                {
                                    TestResultEntryCategory category;
                                    switch (x.Event.Type)
                                    {
                                        case "Log":
                                        case "Display":
                                            category = TestResultEntryCategory.Log;
                                            break;
                                        case "Warning":
                                            category = TestResultEntryCategory.Warning;
                                            break;
                                        case "Error":
                                            category = TestResultEntryCategory.Error;
                                            break;
                                        default:
                                            return null;
                                    }
                                    return new TestResultEntry
                                    {
                                        Category = category,
                                        Message = x.Event.Message,
                                        Filename = x.Filename,
                                        LineNumber = x.LineNumber,
                                    };
                                }).Where(x => x != null).ToArray()!;
                                _testLogger.LogFinished(worker, nextTest);
                                _notification.TestFinished(nextTest);
                                return Task.FromResult(true);
                            }

                            return Task.FromResult(false);
                        }, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        nextTest.TestStatus = TestResultStatus.Cancelled;
                        workerGroupState.ProcessedTests.Add(nextTest);
                        break;
                    }
                    catch (Exception ex)
                    {
                        nextTest.TestStatus = TestResultStatus.Crashed;
                        nextTest.AutomationRunnerCrashInfo = ex;
                        _testLogger.LogException(worker, ex, "Automation runner exception");
                        workerGroupState.ProcessedTests.Add(nextTest);
                    }
                    finally
                    {
                        workerState.CurrentTest = null;
                        workerGroupState.RemainingTests--;
                    }
                }
            }
            finally
            {
                await workerState.Handle.DisposeAsync();
            }
        }

        private async Task NegotiateWithWorkerAsync(IWorker worker)
        {
            var workerState = _workers[worker];
            var connection = workerState.TransportConnection;

            // Detect the remote engine's version so we can pretend to be the same.
            bool gotEngineVersion = false, gotSessionId = false;
            _logger.LogTrace("Sending EngineServicePing");
            connection.Send(new EngineServicePing());
            _logger.LogTrace("Sending SessionServicePing");
            connection.Send(new SessionServicePing { UserName = string.Empty });
            _logger.LogTrace("Waiting for EngineServicePong and SessionServicePong");
            await connection.ReceiveUntilAsync(message =>
            {
                var obj = message.GetMessageData();
                switch (obj)
                {
                    case SessionServicePing ping:
                        // Send our pings again. We can end up sending our EngineServicePing too early
                        // for Unreal to respond to them, but we still need to make the Send calls
                        // before so we at least do the initial negotiation. If our pings got dropped,
                        // this ensures that we send them again when the engine starts responding.
                        _logger.LogTrace("Received SessionServicePing, re-sending intro");
                        connection.Send(new EngineServicePing());
                        connection.Send(new SessionServicePing { UserName = string.Empty });
                        return Task.FromResult(false);
                    case EngineServicePong pong:
                        _logger.LogTrace("Received EngineServicePong");
                        workerState.NegotiatedEngineVersion = pong.EngineVersion;
                        gotEngineVersion = true;
                        return Task.FromResult(gotEngineVersion && gotSessionId);
                    case SessionServicePong pong:
                        _logger.LogTrace("Received SessionServicePong");
                        workerState.NegotiatedSessionId = pong.SessionId;
                        workerState.NegotiatedBuildDate = pong.BuildDate;
                        workerState.NegotiatedSessionOwner = pong.SessionOwner;
                        gotSessionId = true;
                        return Task.FromResult(gotEngineVersion && gotSessionId);
                    default:
                        _logger.LogTrace($"Received object of type {obj.GetType().FullName}");
                        break;
                }

                return Task.FromResult(false);
            }, _cancellationTokenSource.Token);

            // Find the worker "address".
            _logger.LogTrace("Sending AutomationWorkerFindWorkers");
            connection.Send(new AutomationWorkerFindWorkers
            {
                Changelist = 10000,
                GameName = worker.Descriptor.Target,
                ProcessName = "UETAutomation",
                SessionId = workerState.NegotiatedSessionId,
            });
            _logger.LogTrace("Waiting for AutomationWorkerFindWorkersResponse");
            await connection.ReceiveUntilAsync(message =>
            {
                switch (message.GetMessageData())
                {
                    case AutomationWorkerFindWorkersResponse response:
                        _logger.LogTrace("Received AutomationWorkerFindWorkersResponse");
                        workerState.NegotiatedSessionId = response.SessionId;
                        workerState.NegotiatedTargetAddress = message.SenderAddress.V;
                        return Task.FromResult(true);
                }

                return Task.FromResult(false);
            }, _cancellationTokenSource.Token);
        }

        private Task WorkerRemovedFromPoolAsync(IWorker worker, int exitCode, IWorkerCrashData? crashData)
        {
            if (crashData != null)
            {
                if (_workers.ContainsKey(worker))
                {
                    var currentTest = _workers[worker].CurrentTest;
                    if (currentTest != null)
                    {
                        currentTest.EngineCrashInfo = crashData.CrashErrorMessage;
                        currentTest.TestStatus = TestResultStatus.Crashed;
                    }
                    _workers.Remove(worker);
                }
            }
            return Task.CompletedTask;
        }

        private Task WorkerPoolFailureAsync(string reason)
        {
            _cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }

        private async Task RunAsync()
        {
            try
            {
                await using (_workerPool = await _workerPoolFactory.CreateAndStartAsync(
                    _workerDescriptors,
                    WorkerAddedToPoolAsync,
                    WorkerRemovedFromPoolAsync,
                    WorkerPoolFailureAsync,
                    _cancellationTokenSource.Token))
                {
                    while (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        // Have we found tests for all descriptors?
                        var foundAll = true;
                        foreach (var descriptor in _workerDescriptors)
                        {
                            if (!_tests.ContainsKey(descriptor))
                            {
                                foundAll = false;
                                break;
                            }
                        }
                        if (!foundAll)
                        {
                            _logger.LogTrace("Still waiting for all tests to be discovered...");
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                            continue;
                        }

                        // Have we finished tests for all descriptors?
                        var ranAll = true;
                        foreach (var kv in _tests)
                        {
                            if (kv.Value.RemainingTests > 0)
                            {
                                ranAll = false;
                                break;
                            }
                        }
                        if (!ranAll)
                        {
                            _logger.LogTrace($"Still waiting for all tests to run ({_tests.Values.Sum(x => x.RemainingTests)} to go)...");
                            await Task.Delay(1000, _cancellationTokenSource.Token);
                            continue;
                        }

                        // We've found and run tests on all the worker groups.
                        break;
                    }
                }
            }
            finally
            {
                // Go through all the test results and report them.
                await _reporter.ReportResultsAsync(
                    _tests.Values.SelectMany(x => x.AllTests).ToArray());
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    await _timeoutTask;
                }
                catch (OperationCanceledException)
                {
                }
                try
                {
                    await _runTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
            finally
            {
                if (_workerPool != null)
                {
                    await _workerPool.DisposeAsync();
                }
            }
        }

        public async Task<TestResult[]> WaitForResultsAsync()
        {
            await _runTask;
            return _tests.Values.SelectMany(x => x.AllTests).ToArray();
        }
    }
}
