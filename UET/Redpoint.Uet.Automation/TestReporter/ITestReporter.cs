namespace Redpoint.Uet.Automation.TestReporter
{
    using Redpoint.Uet.Automation.Model;
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
