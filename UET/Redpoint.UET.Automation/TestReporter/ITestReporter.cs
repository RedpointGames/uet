namespace Redpoint.UET.Automation.TestReporter
{
    using Redpoint.UET.Automation.Model;
    using System.Threading.Tasks;

    public interface ITestReporter
    {
        Task ReportResultsAsync(
            string projectName,
            TestResult[] results,
            TimeSpan duration,
            string filenamePrefixToCut);
    }
}
