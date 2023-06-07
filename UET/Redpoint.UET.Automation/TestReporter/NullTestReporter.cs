namespace Redpoint.UET.Automation.TestReporter
{
    using Redpoint.UET.Automation.Model;
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
