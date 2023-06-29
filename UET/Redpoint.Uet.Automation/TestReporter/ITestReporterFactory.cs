namespace Redpoint.Uet.Automation.TestReporter
{
    public interface ITestReporterFactory
    {
        ITestReporter CreateNull();

        ITestReporter CreateJunit(string path);
    }
}
