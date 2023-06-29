namespace Redpoint.Uet.Automation.TestReporter
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Automation.Model;
    using System.Threading.Tasks;

    internal class NullTestReporter : ITestReporter
    {
        private readonly ILogger<NullTestReporter> _logger;

        public NullTestReporter(
            ILogger<NullTestReporter> logger)
        {
            _logger = logger;
        }

        public Task ReportResultsAsync(
            string projectName,
            TestResult[] results,
            TimeSpan duration,
            string filenamePrefixToCut)
        {
            _logger.LogTrace($"Skipping writing test results because the null reporter is in use.");
            return Task.CompletedTask;
        }
    }
}
