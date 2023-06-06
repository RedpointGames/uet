namespace Redpoint.UET.Automation.TestReporter
{
    using Redpoint.UET.Automation.Model;
    using System;
    using System.Threading.Tasks;

    internal class JunitTestReporter : ITestReporter
    {
        private readonly string _path;

        public JunitTestReporter(string path)
        {
            _path = path;
        }

        public Task ReportResultsAsync(TestResult[] results)
        {
            throw new NotImplementedException();
        }
    }
}
