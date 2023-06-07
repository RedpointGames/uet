namespace Redpoint.UET.Automation.TestLogger
{
    using GrpcDotNetNamedPipes;
    using Redpoint.UET.Automation.Model;
    using Redpoint.UET.Automation.TestLogging;
    using Redpoint.UET.Automation.Worker;
    using System;
    using System.Threading.Tasks;
    using UETAutomation;

    internal class GrpcTestLogger : ITestLogger
    {
        private readonly TestReporting.TestReportingClient _client;

        public GrpcTestLogger(string pipeName)
        {
            var channel = new NamedPipeChannel(".", pipeName);
            _client = new TestReporting.TestReportingClient(channel);
        }

        public async Task LogDiscovered(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            await _client.LogTestDiscoveredAsync(new LogTestDiscoveredRequest
            {
                TestsRemaining = progressionInfo.TestsRemaining,
                TestsTotal = progressionInfo.TestsTotal,
                WorkerDisplayName = worker.DisplayName ?? string.Empty,
                FullTestPath = testResult.FullTestPath ?? string.Empty,
            });
        }

        public async Task LogException(IWorker worker, TestProgressionInfo progressionInfo, Exception exception, string context)
        {
            await _client.LogRunnerExceptionAsync(new LogRunnerExceptionRequest
            {
                TestsRemaining = progressionInfo.TestsRemaining,
                TestsTotal = progressionInfo.TestsTotal,
                WorkerDisplayName = worker.DisplayName ?? string.Empty,
                ExceptionText = exception.ToString() ?? string.Empty,
                ExceptionContext = context ?? string.Empty,
            });
        }

        public async Task LogFinished(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            await _client.LogTestFinishedAsync(new LogTestFinishedRequest
            {
                TestsRemaining = progressionInfo.TestsRemaining,
                TestsTotal = progressionInfo.TestsTotal,
                WorkerDisplayName = worker.DisplayName ?? string.Empty,
                FullTestPath = testResult.FullTestPath ?? string.Empty,
                Status = Convert(testResult.TestStatus),
                DurationSeconds = testResult.Duration.TotalSeconds,
            });
        }

        public async Task LogStarted(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            await _client.LogTestStartedAsync(new LogTestStartedRequest
            {
                TestsRemaining = progressionInfo.TestsRemaining,
                TestsTotal = progressionInfo.TestsTotal,
                WorkerDisplayName = worker.DisplayName ?? string.Empty,
                FullTestPath = testResult.FullTestPath ?? string.Empty,
            });
        }

        public async Task LogWorkerStarted(IWorker worker, TimeSpan startupDuration)
        {
            await _client.LogWorkerStartedAsync(new LogWorkerStartedRequest
            {
                WorkerDisplayName = worker.DisplayName ?? string.Empty,
                StartupDurationSeconds = startupDuration.TotalSeconds,
            });
        }

        public async Task LogWorkerStarting(IWorker worker)
        {
            await _client.LogWorkerStartingAsync(new LogWorkerStartingRequest
            {
                WorkerDisplayName = worker.DisplayName ?? string.Empty,
            });
        }

        public async Task LogWorkerStopped(IWorker worker, IWorkerCrashData? workerCrashData)
        {
            await _client.LogWorkerStoppedAsync(new LogWorkerStoppedRequest
            {
                WorkerDisplayName = worker.DisplayName ?? string.Empty,
                WorkerHasCrashData = workerCrashData != null,
                WorkerCrashData = workerCrashData?.CrashErrorMessage ?? string.Empty,
            });
        }

        private UETAutomation.TestResultStatus Convert(Model.TestResultStatus testStatus)
        {
            switch (testStatus)
            {
                case Model.TestResultStatus.NotRun:
                    return UETAutomation.TestResultStatus.NotRun;
                case Model.TestResultStatus.InProgress:
                    return UETAutomation.TestResultStatus.InProgress;
                case Model.TestResultStatus.Passed:
                    return UETAutomation.TestResultStatus.Passed;
                case Model.TestResultStatus.Failed:
                    return UETAutomation.TestResultStatus.Failed;
                case Model.TestResultStatus.Cancelled:
                    return UETAutomation.TestResultStatus.Cancelled;
                case Model.TestResultStatus.Skipped:
                    return UETAutomation.TestResultStatus.Skipped;
                case Model.TestResultStatus.Crashed:
                    return UETAutomation.TestResultStatus.Crashed;
            }
            return UETAutomation.TestResultStatus.NotRun;
        }
    }
}
