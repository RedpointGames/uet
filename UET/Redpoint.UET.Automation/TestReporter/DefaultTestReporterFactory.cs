namespace Redpoint.UET.Automation.TestReporter
{
    internal class DefaultTestReporterFactory : ITestReporterFactory
    {
        public ITestReporter CreateJunit(string path)
        {
            return new JunitTestReporter(path);
        }

        public ITestReporter CreateNull()
        {
            return new NullTestReporter();
        }
    }
}
