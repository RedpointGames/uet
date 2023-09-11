namespace Redpoint.Uet.Automation.TestLogging
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Automation.Model;
    using Redpoint.Uet.Automation.Worker;
    using System;
    using static Crayon.Output;

    internal sealed class ConsoleTestLogger : ITestLogger
    {
        private readonly ILogger<ConsoleTestLogger> _logger;

        public ConsoleTestLogger(ILogger<ConsoleTestLogger> logger)
        {
            _logger = logger;
        }

        private string Worker(IWorker worker)
        {
            if (worker == null)
            {
                return string.Empty;
            }
            return $"[Worker {worker.DisplayName}] ";
        }

        private string Progress(TestProgressionInfo? progressionInfo)
        {
            // [100%, 1234/1234]

            if (progressionInfo == null)
            {
                return "[               ]";
            }

            int doneSoFar = progressionInfo.TestsTotal - progressionInfo.TestsRemaining;
            double percent = 0.0;
            if (progressionInfo.TestsTotal > 0)
            {
                percent = Math.Round(doneSoFar / (double)progressionInfo.TestsTotal * 100.0);
            }

            return $"[{percent,3:0}%, {doneSoFar,4}/{progressionInfo.TestsTotal,4}]";
        }

        public Task LogWorkerStarting(IWorker worker)
        {
            _logger.LogInformation($"{Progress(null)} {Worker(worker)}Worker starting...");
            return Task.CompletedTask;
        }

        public Task LogWorkerStarted(IWorker worker, TimeSpan startupDuration)
        {
            _logger.LogInformation($"{Progress(null)} {Worker(worker)}Worker started in {startupDuration.TotalSeconds} seconds.");
            return Task.CompletedTask;
        }

        public Task LogWorkerStopped(IWorker worker, IWorkerCrashData? workerCrashData)
        {
            _logger.LogInformation($"{Progress(null)} {Worker(worker)}Worker stopped");
            return Task.CompletedTask;
        }

        public Task LogDiscovered(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Cyan("[discovered]")}");
            return Task.CompletedTask;
        }

        public Task LogStarted(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Bright.Black("[started]")}");
            return Task.CompletedTask;
        }

        public Task LogFinished(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            foreach (var warn in testResult.Entries.Where(x => x.Category == TestResultEntryCategory.Warning))
            {
                _logger.LogWarning($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {warn.Message.Trim()}");
            }
            foreach (var error in testResult.Entries.Where(x => x.Category == TestResultEntryCategory.Error))
            {
                _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {error.Message.Trim()}");
            }
            if (testResult.AutomationRunnerCrashInfo != null)
            {
                _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {testResult.AutomationRunnerCrashInfo}");
            }
            if (testResult.EngineCrashInfo != null)
            {
                foreach (var line in testResult.EngineCrashInfo.Split('\n'))
                {
                    _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {line}");
                }
            }
            switch (testResult.TestStatus)
            {
                case TestResultStatus.NotRun:
                    _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Bright.Blue("[not run]")}");
                    break;
                case TestResultStatus.InProgress:
                    _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Bright.Blue("[in progress]")}");
                    break;
                case TestResultStatus.Passed:
                    _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Bright.Green($"[passed in {testResult.Duration.TotalSeconds:0.##} secs]")}");
                    break;
                case TestResultStatus.Failed:
                    _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Bright.Red($"[failed in {testResult.Duration.TotalSeconds:0.##} secs]")}");
                    break;
                case TestResultStatus.Skipped:
                    _logger.LogWarning($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Bright.Yellow($"[skipped in {testResult.Duration.TotalSeconds:0.##} secs]")}");
                    break;
                case TestResultStatus.Cancelled:
                    _logger.LogWarning($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Bright.Yellow($"[cancelled in {testResult.Duration.TotalSeconds:0.##} secs]")}");
                    break;
                case TestResultStatus.Crashed:
                    _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Bright.Red($"[crashed in {testResult.Duration.TotalSeconds:0.##} secs]")}");
                    break;
                case TestResultStatus.TimedOut:
                    _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {Bright.Red($"[timed out in {testResult.Duration.TotalSeconds:0.##} secs]")}");
                    break;
            }
            return Task.CompletedTask;
        }

        public Task LogException(IWorker worker, TestProgressionInfo progressionInfo, Exception exception, string context)
        {
            _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}[exception] {context}: ({exception.GetType().FullName}) {exception.Message}");
            _logger.LogError(exception.StackTrace);
            return Task.CompletedTask;
        }

        public Task LogTestRunTimedOut(TimeSpan testRunTimeout)
        {
            _logger.LogError($"Test run exceeded timeout of {testRunTimeout.TotalMinutes:0.##} minutes, so the test run is being cancelled. If you're using a BuildConfig.json file, you can increase the overall timeout by setting the 'TestRunTimeoutMinutes' property.");
            return Task.CompletedTask;
        }
    }
}