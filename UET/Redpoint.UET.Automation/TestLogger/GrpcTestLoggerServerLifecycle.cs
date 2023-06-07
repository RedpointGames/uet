namespace Redpoint.UET.Automation.TestLogger
{
    using Grpc.Core;
    using GrpcDotNetNamedPipes;
    using Redpoint.UET.Automation.Model;
    using Redpoint.UET.Automation.TestLogging;
    using Redpoint.UET.Automation.Worker;
    using Redpoint.UET.Automation.Worker.Local;
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using UETAutomation;

    internal class GrpcTestLoggerServerLifecycle : IAutomationLogForwarder
    {
        private readonly ITestLoggerFactory _testLoggerFactory;
        private GrpcTestLoggerServer? _loggerServer;

        public GrpcTestLoggerServerLifecycle(
            ITestLoggerFactory testLoggerFactory)
        {
            _testLoggerFactory = testLoggerFactory;
        }

        public string? GetPipeName()
        {
            return _loggerServer?.PipeName;
        }

        public Task StartAsync(CancellationToken shutdownCancellationToken)
        {
            _loggerServer = new GrpcTestLoggerServer(_testLoggerFactory);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (_loggerServer != null)
            {
                _loggerServer.Stop();
            }
            _loggerServer = null;
            return Task.CompletedTask;
        }

        private class GrpcTestLoggerServer : TestReporting.TestReportingBase
        {
            private class FakeWorker : IWorker
            {
                private readonly string _name;

                public FakeWorker(string name)
                {
                    _name = name;
                }

                public string Id => throw new NotImplementedException();

                public string DisplayName => _name;

                public DesiredWorkerDescriptor Descriptor => throw new NotImplementedException();

                public IPEndPoint EndPoint => throw new NotImplementedException();

                public TimeSpan StartupDuration => throw new NotImplementedException();
            }

            private readonly NamedPipeServer _pipeServer;
            private readonly ITestLogger _testLogger;

            public string PipeName { get; private set; }

            public GrpcTestLoggerServer(ITestLoggerFactory testLoggerFactory)
            {
                PipeName = $"UETAutomationLog-{BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", "").ToLowerInvariant()}";
                _pipeServer = new NamedPipeServer(PipeName);
                TestReporting.BindService(_pipeServer.ServiceBinder, this);
                _pipeServer.Start();
                _testLogger = testLoggerFactory.CreateConsole();
            }

            public void Stop()
            {
                _pipeServer.Kill();
            }

            public override async Task<LogResponse> LogWorkerStarting(LogWorkerStartingRequest request, ServerCallContext context)
            {
                await _testLogger.LogWorkerStarting(new FakeWorker(request.WorkerDisplayName));
                return new LogResponse();
            }

            public override async Task<LogResponse> LogWorkerStarted(LogWorkerStartedRequest request, ServerCallContext context)
            {
                await _testLogger.LogWorkerStarted(new FakeWorker(request.WorkerDisplayName), TimeSpan.FromSeconds(request.StartupDurationSeconds));
                return new LogResponse();
            }

            public override async Task<LogResponse> LogWorkerStopped(LogWorkerStoppedRequest request, ServerCallContext context)
            {
                await _testLogger.LogWorkerStopped(
                    new FakeWorker(request.WorkerDisplayName),
                    request.WorkerHasCrashData ? new LocalWorkerCrashData(request.WorkerCrashData) : null);
                return new LogResponse();
            }

            public override async Task<LogResponse> LogTestDiscovered(LogTestDiscoveredRequest request, ServerCallContext context)
            {
                await _testLogger.LogDiscovered(
                    new FakeWorker(request.WorkerDisplayName),
                    new TestProgressionInfo
                    {
                        TestsRemaining = request.TestsRemaining,
                        TestsTotal = request.TestsTotal,
                    },
                    new TestResult
                    {
                        FullTestPath = request.FullTestPath,
                        TestStatus = Model.TestResultStatus.NotRun,
                        DateStarted = DateTime.UtcNow,
                        DateFinished = DateTime.UtcNow,
                        Duration = TimeSpan.Zero,
                        Entries = new TestResultEntry[0],
                        Platform = string.Empty,
                        TestName = string.Empty,
                        WorkerDisplayName = request.WorkerDisplayName,
                    });
                return new LogResponse();
            }

            public override async Task<LogResponse> LogTestStarted(LogTestStartedRequest request, ServerCallContext context)
            {
                await _testLogger.LogStarted(
                    new FakeWorker(request.WorkerDisplayName),
                    new TestProgressionInfo
                    {
                        TestsRemaining = request.TestsRemaining,
                        TestsTotal = request.TestsTotal,
                    },
                    new TestResult
                    {
                        FullTestPath = request.FullTestPath,
                        TestStatus = Model.TestResultStatus.NotRun,
                        DateStarted = DateTime.UtcNow,
                        DateFinished = DateTime.UtcNow,
                        Duration = TimeSpan.Zero,
                        Entries = new TestResultEntry[0],
                        Platform = string.Empty,
                        TestName = string.Empty,
                        WorkerDisplayName = request.WorkerDisplayName,
                    });
                return new LogResponse();
            }

            public override async Task<LogResponse> LogTestFinished(LogTestFinishedRequest request, ServerCallContext context)
            {
                var testResult = new TestResult
                {
                    FullTestPath = request.FullTestPath,
                    TestStatus = Convert(request.Status),
                    DateStarted = DateTime.UtcNow,
                    DateFinished = DateTime.UtcNow,
                    Duration = TimeSpan.FromSeconds(request.DurationSeconds),
                    Entries = request.Warnings.Select(x => new TestResultEntry
                    {
                        Category = TestResultEntryCategory.Warning,
                        Message = x,
                        LineNumber = 0,
                        Filename = string.Empty,
                    }).Concat(request.Errors.Select(x => new TestResultEntry
                    {
                        Category = TestResultEntryCategory.Error,
                        Message = x,
                        LineNumber = 0,
                        Filename = string.Empty,
                    })).ToArray(),
                    Platform = string.Empty,
                    TestName = string.Empty,
                    WorkerDisplayName = request.WorkerDisplayName,
                };
                if (!string.IsNullOrWhiteSpace(request.AutomationRunnerCrashInfo))
                {
                    testResult.AutomationRunnerCrashInfo = new Exception(request.AutomationRunnerCrashInfo);
                }
                if (!string.IsNullOrWhiteSpace(request.EngineCrashInfo))
                {
                    testResult.EngineCrashInfo = request.EngineCrashInfo;
                }
                await _testLogger.LogFinished(
                    new FakeWorker(request.WorkerDisplayName),
                    new TestProgressionInfo
                    {
                        TestsRemaining = request.TestsRemaining,
                        TestsTotal = request.TestsTotal,
                    },
                    testResult);
                return new LogResponse();
            }

            public override async Task<LogResponse> LogRunnerException(LogRunnerExceptionRequest request, ServerCallContext context)
            {
                await _testLogger.LogException(
                    new FakeWorker(request.WorkerDisplayName),
                    new TestProgressionInfo
                    {
                        TestsRemaining = request.TestsRemaining,
                        TestsTotal = request.TestsTotal,
                    },
                    new Exception($"Forwarded exception: {request.ExceptionText}"),
                    request.ExceptionContext);
                return new LogResponse();
            }

            private Model.TestResultStatus Convert(UETAutomation.TestResultStatus testStatus)
            {
                switch (testStatus)
                {
                    case UETAutomation.TestResultStatus.NotRun:
                        return Model.TestResultStatus.NotRun;
                    case UETAutomation.TestResultStatus.InProgress:
                        return Model.TestResultStatus.InProgress;
                    case UETAutomation.TestResultStatus.Passed:
                        return Model.TestResultStatus.Passed;
                    case UETAutomation.TestResultStatus.Failed:
                        return Model.TestResultStatus.Failed;
                    case UETAutomation.TestResultStatus.Cancelled:
                        return Model.TestResultStatus.Cancelled;
                    case UETAutomation.TestResultStatus.Skipped:
                        return Model.TestResultStatus.Skipped;
                    case UETAutomation.TestResultStatus.Crashed:
                        return Model.TestResultStatus.Crashed;
                    case UETAutomation.TestResultStatus.TimedOut:
                        return Model.TestResultStatus.TimedOut;
                }
                return Model.TestResultStatus.NotRun;
            }
        }
    }
}
