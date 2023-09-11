namespace Redpoint.Uet.Automation.Runner
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Uet.Automation.Model;
    using Redpoint.Uet.Automation.TestLogging;
    using Redpoint.Uet.Automation.TestNotification;
    using Redpoint.Uet.Automation.TestReporter;
    using Redpoint.Uet.Automation.Worker;
    using Redpoint.Unreal.Serialization;
    using Redpoint.Unreal.TcpMessaging;
    using Redpoint.Unreal.TcpMessaging.MessageTypes;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Sockets;
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
        private readonly AutomationRunnerConfiguration _configuration;
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

            public MessageAddress? NegotiatedTargetAddress { get; set; }

            public Task? TestingTask { get; set; }

            public required IAsyncDisposable Handle { get; set; }

            public required CancellationTokenSource CancellationTokenSource { get; set; }

            public required Stopwatch StartupStopwatch { get; set; }
        }

        private class WorkerGroupState
        {
            public required DesiredWorkerDescriptor Descriptor { get; set; }

            public IReadOnlyList<TestResult> AllTests { get; set; } = new List<TestResult>();

            public ConcurrentQueue<TestResult> QueuedTests { get; } = new ConcurrentQueue<TestResult>();

            public ConcurrentBag<TestResult> ProcessedTests { get; } = new ConcurrentBag<TestResult>();

            public int RemainingTests { get; set; }

            public Gate ReadyForTesting { get; } = new Gate();
        }

        public DefaultAutomationRunner(
            ILogger<DefaultAutomationRunner> logger,
            IWorkerPoolFactory workerPoolFactory,
            ITestLogger testLogger,
            ITestNotification notification,
            ITestReporter reporter,
            IEnumerable<DesiredWorkerDescriptor> workerGroups,
            AutomationRunnerConfiguration configuration,
            CancellationToken cancellationToken)
        {
            _logger = logger;
            _workerPoolFactory = workerPoolFactory;
            _testLogger = testLogger;
            _notification = notification;
            _reporter = reporter;
            _workerDescriptors = workerGroups.ToArray();
            _configuration = configuration;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _tests = new ConcurrentDictionary<DesiredWorkerDescriptor, WorkerGroupState>();
            _workers = new Dictionary<IWorker, WorkerState>();
            _timeoutTask = Task.Run(TimeoutAsync, cancellationToken);
            _runTask = Task.Run(RunAsync, cancellationToken);
        }

        private async Task TimeoutAsync()
        {
            if (_configuration.TestRunTimeout.HasValue && _configuration.TestRunTimeout.Value != TimeSpan.MaxValue)
            {
                await Task.Delay((int)_configuration.TestRunTimeout.Value.TotalMilliseconds, _cancellationTokenSource.Token).ConfigureAwait(false);
                await _testLogger.LogTestRunTimedOut(_configuration.TestRunTimeout.Value).ConfigureAwait(false);
                _cancellationTokenSource.Cancel();
            }
        }

        private TestProgressionInfo GetProgressionInfo()
        {
            return new TestProgressionInfo
            {
                TestsRemaining = _tests.Values.Sum(x => x.RemainingTests),
                TestsTotal = _tests.Values.Sum(x => x.AllTests.Count),
            };
        }

        private async Task WorkerAddedToPoolAsync(IWorker worker)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogTrace($"Received new worker {worker.Id} to pool");

            // Connect to the worker and add it.
            _logger.LogTrace($"Setting up Unreal TCP transport connection...");
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            WorkerState workerState;
            try
            {
                workerState = new WorkerState
                {
                    StartupStopwatch = stopwatch,
                    Handle = await _workerPool!.ReserveAsync(worker).ConfigureAwait(false),
                    CancellationTokenSource = cts,
                    TransportConnection = await TcpMessageTransportConnection.CreateAsync(async () =>
                    {
                        // Connect to the TCP socket.
                        TcpClient? tcpClient = null;
                        do
                        {
                            _logger.LogTrace($"Attempting to connect to worker {worker.Id} on endpoint {worker.EndPoint}");
                            tcpClient = new TcpClient();
                            var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                            var connectionTask = tcpClient.ConnectAsync(worker.EndPoint, connectionCts.Token).AsTask();
                            await Task.WhenAny(connectionTask, Task.Delay(1000)).ConfigureAwait(false);
                            if (!tcpClient.Connected)
                            {
                                _logger.LogTrace($"Failed to connect to worker {worker.Id} on endpoint {worker.EndPoint} with 1 seconds, retrying");
                                connectionCts.Cancel();
                                continue;
                            }
                            break;
                        } while (!cts.Token.IsCancellationRequested);
                        cts.Token.ThrowIfCancellationRequested();
                        _logger.LogTrace($"Successfully connected to worker {worker.Id} on endpoint {worker.EndPoint}");
                        return tcpClient;
                    }, _logger).ConfigureAwait(false),
                    CurrentTest = null,
                };
            }
            catch (IOException ex) when (ex.InnerException is SocketException && ((SocketException)ex.InnerException).SocketErrorCode == SocketError.ConnectionReset && _cancellationTokenSource.IsCancellationRequested)
            {
                // The user pressed Ctrl-C while a worker was starting up.
                return;
            }
            _workers.Add(worker, workerState);
            var connection = workerState.TransportConnection;

            connection.OnUnrecoverablyBroken += (_, _) =>
            {
                _logger.LogError("Worker died due to TCP transport error. Killing worker.");
                _workerPool!.KillWorker(worker);
            };

            _logger.LogTrace($"Connected to worker {worker.Id}");

            try
            {
                // Negotiate with the worker connection so we can start sending messages to it.
                _logger.LogTrace($"Starting negotiation with worker {worker.Id}");
                await NegotiateWithWorkerAsync(worker).ConfigureAwait(false);

                // We intentionally don't emit the LogWorkerStarted until now, because there can be quite
                // some time between making the TCP connection and actually being able to communicate.
                workerState.StartupStopwatch.Stop();
                await _testLogger.LogWorkerStarted(worker, worker.StartupDuration + workerState.StartupStopwatch.Elapsed).ConfigureAwait(false);

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
                        }, CancellationToken.None).ConfigureAwait(false);
                        _logger.LogTrace($"Received {discoveredTests.Tests.Count} tests");

                        // Generate all of the pending tests.
                        groupState.AllTests = discoveredTests.Tests
                            .Where(x => x.FullTestPath.StartsWith(_configuration.TestPrefix))
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
                                Entries = Array.Empty<TestResultEntry>(),
                            }).ToList();
                        foreach (var testToProcess in groupState.AllTests)
                        {
                            _notification.TestDiscovered(testToProcess);
                            groupState.RemainingTests++;
                            groupState.QueuedTests.Enqueue(testToProcess);
                        }
                        foreach (var testToProcess in groupState.AllTests)
                        {
                            await _testLogger.LogDiscovered(worker, GetProgressionInfo(), testToProcess).ConfigureAwait(false);
                        }

                        // We are now ready to process tests.
                        _logger.LogTrace($"Ready to begin testing for descriptor {groupState.Descriptor.Platform}");
                        groupState.ReadyForTesting.Open();
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
                    await RunWorkerAsync(worker).ConfigureAwait(false);
                });
            }
            catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
            {
                // The user pressed Ctrl-C during startup.
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected exception during worker startup: {ex.Message}");
                await workerState.Handle.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private async Task RunWorkerAsync(IWorker worker)
        {
            var workerGroupState = _tests[worker.Descriptor];
            var workerState = _workers[worker];

            var workerCancellationToken = workerState.CancellationTokenSource.Token;

            try
            {
                _logger.LogTrace($"Waiting for testing to be ready...");
                await workerGroupState.ReadyForTesting.WaitAsync(workerCancellationToken).ConfigureAwait(false);

                _logger.LogTrace($"Testing loop for worker {worker.Id} is now starting...");

                while (!workerCancellationToken.IsCancellationRequested)
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
                    nextTest.AttemptCount++;
                    workerState.CurrentTest = nextTest;
                    await _testLogger.LogStarted(worker, GetProgressionInfo(), nextTest).ConfigureAwait(false);
                    _notification.TestStarted(nextTest);
                    try
                    {
                        CancellationToken tokenWithTimeout;
                        if (_configuration.TestTimeout.HasValue && _configuration.TestTimeout.Value != TimeSpan.MaxValue)
                        {
                            tokenWithTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                                workerCancellationToken,
                                new CancellationTokenSource(_configuration.TestTimeout.Value).Token).Token;
                        }
                        else
                        {
                            tokenWithTimeout = workerCancellationToken;
                        }

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
                        await workerState.TransportConnection.ReceiveUntilAsync(async message =>
                        {
                            var data = message.GetMessageData();
                            var reply = data as AutomationWorkerRunTestsReply;
                            if (reply == null)
                            {
                                return false;
                            }

                            if (reply.TestName != nextTest.TestName)
                            {
                                return false;
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

                                if (nextTest.TestStatus == TestResultStatus.Failed &&
                                    _configuration.TestAttemptCount.HasValue &&
                                    nextTest.AttemptCount < _configuration.TestAttemptCount.Value)
                                {
                                    // Let things know that this test failed, so they don't count
                                    // it as running anymore.
                                    await _testLogger.LogFinished(worker, GetProgressionInfo(), nextTest).ConfigureAwait(false);
                                    _notification.TestFinished(nextTest);

                                    // This test needs to go back in the queue.
                                    workerGroupState.QueuedTests.Enqueue(nextTest);
                                }
                                else
                                {
                                    // This test is done.
                                    workerGroupState.RemainingTests--;
                                    workerGroupState.ProcessedTests.Add(nextTest);
                                    await _testLogger.LogFinished(worker, GetProgressionInfo(), nextTest).ConfigureAwait(false);
                                    _notification.TestFinished(nextTest);
                                }

                                return true;
                            }

                            return false;
                        }, tokenWithTimeout).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!workerCancellationToken.IsCancellationRequested)
                    {
                        // This test timed out.
                        nextTest.Duration = _configuration.TestTimeout ?? TimeSpan.Zero;
                        nextTest.DateFinished = DateTimeOffset.UtcNow;
                        nextTest.TestStatus = TestResultStatus.TimedOut;

                        // Should we retry this test?
                        if (_configuration.TestAttemptCount.HasValue &&
                            nextTest.AttemptCount < _configuration.TestAttemptCount.Value)
                        {
                            // This test needs to go back in the queue.
                            await _testLogger.LogFinished(worker, GetProgressionInfo(), nextTest).ConfigureAwait(false);
                            _notification.TestFinished(nextTest);
                            workerGroupState.QueuedTests.Enqueue(nextTest);
                        }
                        else
                        {
                            // This test has permanently timed out.
                            workerGroupState.RemainingTests--;
                            workerGroupState.ProcessedTests.Add(nextTest);
                            await _testLogger.LogFinished(worker, GetProgressionInfo(), nextTest).ConfigureAwait(false);
                            _notification.TestFinished(nextTest);
                        }

                        // This worker is now "dead" because it will be stuck running a test
                        // that we no longer want it to run.
                        _workerPool!.KillWorker(worker);

                        // We return here because this worker is no longer usable.
                        return;
                    }
                    catch (OperationCanceledException) when (workerCancellationToken.IsCancellationRequested)
                    {
                        // The test status will be updated to Crashed by WorkerRemovedFromPoolAsync before
                        // the background task for the worker is cancelled, so don't override that.
                        if (nextTest.TestStatus != TestResultStatus.Crashed)
                        {
                            // The test run is being cancelled or timed out.
                            nextTest.DateFinished = DateTimeOffset.UtcNow;
                            nextTest.TestStatus = TestResultStatus.Cancelled;
                            nextTest.Duration = nextTest.DateFinished - nextTest.DateStarted;
                            workerGroupState.RemainingTests--;
                            workerGroupState.ProcessedTests.Add(nextTest);
                            await _testLogger.LogFinished(worker, GetProgressionInfo(), nextTest).ConfigureAwait(false);
                            _notification.TestFinished(nextTest);
                        }

                        // Return now because we don't want to continue processing tests.
                        return;
                    }
                    catch (Exception ex)
                    {
                        // Automation exceptions don't get retried.
                        nextTest.AutomationRunnerCrashInfo = ex;
                        nextTest.DateFinished = DateTimeOffset.UtcNow;
                        nextTest.TestStatus = TestResultStatus.Crashed;
                        nextTest.Duration = nextTest.DateFinished - nextTest.DateStarted;
                        workerGroupState.RemainingTests--;
                        workerGroupState.ProcessedTests.Add(nextTest);
                        await _testLogger.LogFinished(worker, GetProgressionInfo(), nextTest).ConfigureAwait(false);
                        _notification.TestFinished(nextTest);
                        await _testLogger.LogException(worker, GetProgressionInfo(), ex, "Automation runner exception").ConfigureAwait(false);

                        // @note: We continue attempting more tests after this.
                    }
                    finally
                    {
                        workerState.CurrentTest = null;
                    }
                }
                workerCancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogTrace($"[{worker.Id}] Worker loop stopping because it was cancelled: {ex}");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"[{worker.Id}] Worker loop stopping because an unexpected exception occurred: {ex}");
            }
            finally
            {
                await workerState.Handle.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task NegotiateWithWorkerAsync(IWorker worker)
        {
            var workerState = _workers[worker];
            var connection = workerState.TransportConnection;

            // Detect the remote engine's version so we can pretend to be the same.
            _logger.LogTrace("Waiting for the connection to be ready...");
            while (connection.RemoteSessionId == null)
            {
                await Task.Delay(500, workerState.CancellationTokenSource.Token).ConfigureAwait(false);
            }
            workerState.NegotiatedSessionId = connection.RemoteSessionId.Value;

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
            }, workerState.CancellationTokenSource.Token).ConfigureAwait(false);
        }

        private async Task WorkerRemovedFromPoolAsync(IWorker worker, int exitCode, IWorkerCrashData? crashData)
        {
            if (_workers.ContainsKey(worker))
            {
                var currentTest = _workers[worker].CurrentTest;
                if (currentTest != null)
                {
                    if (crashData != null)
                    {
                        currentTest.EngineCrashInfo = crashData.CrashErrorMessage;
                    }
                    else
                    {
                        currentTest.EngineCrashInfo = "Engine exited during test without crash dump information!";
                    }
                    currentTest.DateFinished = DateTimeOffset.UtcNow;
                    currentTest.TestStatus = TestResultStatus.Crashed;
                    var workerGroupState = _tests[worker.Descriptor];
                    workerGroupState.RemainingTests--;
                    workerGroupState.ProcessedTests.Add(currentTest);
                    await _testLogger.LogFinished(worker, GetProgressionInfo(), currentTest).ConfigureAwait(false);
                    _notification.TestFinished(currentTest);
                }
                _workers[worker].CancellationTokenSource.Cancel();
                await _workers[worker].TransportConnection.DisposeAsync().ConfigureAwait(false);
                _workers.Remove(worker);
            }
        }

        private Task WorkerPoolFailureAsync(string reason)
        {
            _logger.LogError($"Automation runner was notified of a worker pool failure: {reason}");
            _cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }

        private async Task RunAsync()
        {
            var st = Stopwatch.StartNew();
            try
            {
                await using (_workerPool = await _workerPoolFactory.CreateAndStartAsync(
                    _testLogger,
                    _workerDescriptors,
                    WorkerAddedToPoolAsync,
                    WorkerRemovedFromPoolAsync,
                    WorkerPoolFailureAsync,
                    _cancellationTokenSource.Token).ConfigureAwait(false))
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
                            await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);
                            continue;
                        }

                        // Have we finished tests for all descriptors?
                        var ranAll = true;
                        foreach (var kv in _tests)
                        {
                            if (!kv.Value.ReadyForTesting.Opened || kv.Value.RemainingTests > 0)
                            {
                                ranAll = false;
                                break;
                            }
                        }
                        if (!ranAll)
                        {
                            _logger.LogTrace($"Still waiting for all tests to run ({_tests.Values.Sum(x => x.RemainingTests)} to go)...");
                            await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);
                            continue;
                        }

                        // We've found and run tests on all the worker groups.
                        break;
                    }
                }
            }
            finally
            {
                // Flush all of the data to the notification service.
                _logger.LogTrace("Flushing results to the notification service, as testing has finished...");
                await _notification.FlushAsync().ConfigureAwait(false);
                _logger.LogTrace("Flushed results to the notification service, as testing has finished.");

                // Go through all the test results and report them.
                _logger.LogTrace("Reporting test results to reporter...");
                await _reporter.ReportResultsAsync(
                    _configuration.ProjectName,
                    _tests.Values.SelectMany(x => x.AllTests).ToArray(),
                    st.Elapsed,
                    _configuration.FilenamePrefixToCut).ConfigureAwait(false);
                _logger.LogTrace("Finished reporting test results to reporter.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    await _timeoutTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                try
                {
                    await _runTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            finally
            {
                if (_workerPool != null)
                {
                    await _workerPool.DisposeAsync().ConfigureAwait(false);
                }

                foreach (var kv in _workers)
                {
                    await kv.Value.TransportConnection.DisposeAsync().ConfigureAwait(false);
                }
            }
            _cancellationTokenSource.Dispose();
        }

        public async Task<TestResult[]> WaitForResultsAsync()
        {
            await _runTask.ConfigureAwait(false);
            return _tests.Values.SelectMany(x => x.AllTests).ToArray();
        }
    }
}
