namespace Redpoint.Uet.Automation.TestNotification
{
    using Redpoint.Uet.Automation.Model;
    using System.Threading.Tasks;

    internal class NullTestNotification : ITestNotification
    {
        public void TestDiscovered(TestResult testResult)
        {
        }

        public void TestFinished(TestResult testResult)
        {
        }

        public void TestStarted(TestResult testResult)
        {
        }

        public Task FlushAsync()
        {
            return Task.CompletedTask;
        }
    }
}
