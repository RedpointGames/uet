namespace Redpoint.UET.Automation.TestReporter
{
    public interface ITestReporterFactory
    {
        ITestReporter CreateNull();

        ITestReporter CreateJunit(string path);
    }
}
