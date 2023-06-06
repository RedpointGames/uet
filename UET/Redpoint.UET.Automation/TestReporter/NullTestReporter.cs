namespace Redpoint.UET.Automation.TestReporter
{
    using Redpoint.UET.Automation.Model;
    using System.Threading.Tasks;

    internal class NullTestReporter : ITestReporter
    {
        public Task ReportResultsAsync(TestResult[] results)
        {
            return Task.CompletedTask;
        }
    }
}
