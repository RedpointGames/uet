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

        public ConsoleTestLogger(ILogger<ConsoleTestLogger> logger)
        {
            _logger = logger;
        }

        private string Worker(IWorker worker)
        {
            if (worker == null)
            {
                return "[        ]";
            }
            return $"[Worker {worker.DisplayName}]";
        }

        public void LogDiscovered(IWorker worker, TestResult testResult)
        {
            _logger.LogInformation($"{Worker(worker)} {testResult.FullTestPath} ... {_colorCyan}discovered{_colorReset}");
        }

        public void LogStarted(IWorker worker, TestResult testResult)
        {
            _logger.LogInformation($"{Worker(worker)} {testResult.FullTestPath} ...");
        }

        public void LogFinished(IWorker worker, TestResult testResult)
        {
            switch (testResult.TestStatus)
            {
                case TestResultStatus.NotRun:
                    _logger.LogInformation($"{Worker(worker)} {testResult.FullTestPath} ... {_colorBlue}not run{_colorReset}");
                    break;
                case TestResultStatus.InProgress:
                    _logger.LogInformation($"{Worker(worker)} {testResult.FullTestPath} ... {_colorBlue}in progress{_colorReset}");
                    break;
                case TestResultStatus.Passed:
                    _logger.LogInformation($"{Worker(worker)} {testResult.FullTestPath} ... {_colorGreen}passed{_colorReset} (in {testResult.Duration:0.##} secs)");
                    break;
                case TestResultStatus.Failed:
                    _logger.LogInformation($"{Worker(worker)} {testResult.FullTestPath} ... {_colorRed}failed{_colorReset} (in {testResult.Duration:0.##} secs)");
                    break;
                case TestResultStatus.Skipped:
                    _logger.LogInformation($"{Worker(worker)} {testResult.FullTestPath} ... {_colorYellow}skipped{_colorReset} (in {testResult.Duration:0.##} secs)");
                    break;
                case TestResultStatus.Cancelled:
                    _logger.LogInformation($"{Worker(worker)} {testResult.FullTestPath} ... {_colorYellow}cancelled{_colorReset} (in {testResult.Duration:0.##} secs)");
                    break;
                case TestResultStatus.Crashed:
                    _logger.LogInformation($"{Worker(worker)} {testResult.FullTestPath} ... {_colorRed}crashed{_colorReset} (in {testResult.Duration:0.##} secs)");
                    break;
            }
        }

        public void LogException(IWorker worker, Exception exception, string context)
        {
            _logger.LogError($"{Worker(worker)} [exception] {context}: ({exception.GetType().FullName}) {exception.Message}");
            _logger.LogError(exception.StackTrace);
        }
    }
}