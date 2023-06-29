namespace Redpoint.Uet.Automation.TestReporter
{
    using Redpoint.Uet.Automation.Model;
    using System.Threading.Tasks;

    internal class NullTestReporter : ITestReporter
    {
        public Task ReportResultsAsync(
            string projectName,
            TestResult[] results,
            TimeSpan duration,
            string filenamePrefixToCut)
        {
            return Task.CompletedTask;
        }
    }
}
