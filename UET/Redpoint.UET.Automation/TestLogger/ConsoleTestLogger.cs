namespace Redpoint.UET.Automation.TestLogging
{
    using Microsoft.Extensions.Logging;
    using Redpoint.UET.Automation.Model;
    using Redpoint.UET.Automation.Worker;
    using System;

    internal class ConsoleTestLogger : ITestLogger
    {
        private readonly ILogger<ConsoleTestLogger> _logger;

        private string _colorRed = "\x1b[31m";
        private string _colorGreen = "\x1b[32m";
        private string _colorBlue = "\x1b[34m";
        private string _colorCyan = "\x1b[36m";
        private string _colorYellow = "\x1b[33m";
        private string _colorReset = "\x1b[0m";
        private string _colorGray = "\u001b[38;5;8m";

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
            _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorCyan}[discovered]{_colorReset}");
            return Task.CompletedTask;
        }

        public Task LogStarted(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorGray}[started]{_colorReset}");
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
                    _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorBlue}[not run]{_colorReset}");
                    break;
                case TestResultStatus.InProgress:
                    _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorBlue}[in progress]{_colorReset}");
                    break;
                case TestResultStatus.Passed:
                    _logger.LogInformation($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorGreen}[passed in {testResult.Duration.TotalSeconds:0.##} secs]{_colorReset}");
                    break;
                case TestResultStatus.Failed:
                    _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorRed}[failed in {testResult.Duration.TotalSeconds:0.##} secs]{_colorReset}");
                    break;
                case TestResultStatus.Skipped:
                    _logger.LogWarning($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorYellow}[skipped in {testResult.Duration.TotalSeconds:0.##} secs]{_colorReset}");
                    break;
                case TestResultStatus.Cancelled:
                    _logger.LogWarning($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorYellow}[cancelled in {testResult.Duration.TotalSeconds:0.##} secs]{_colorReset}");
                    break;
                case TestResultStatus.Crashed:
                    _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorRed}[crashed in {testResult.Duration.TotalSeconds:0.##} secs]{_colorReset}");
                    break;
                case TestResultStatus.TimedOut:
                    _logger.LogError($"{Progress(progressionInfo)} {Worker(worker)}{testResult.FullTestPath} {_colorRed}[timed out in {testResult.Duration.TotalSeconds:0.##} secs]{_colorReset}");
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
    }
}