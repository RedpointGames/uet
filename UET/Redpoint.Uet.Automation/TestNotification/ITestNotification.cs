namespace Redpoint.Uet.Automation.TestNotification
{
    using Redpoint.Uet.Automation.Model;

    public interface ITestNotification
    {
        void TestDiscovered(TestResult testResult);

        void TestStarted(TestResult testResult);

        void TestFinished(TestResult testResult);

        Task FlushAsync();
    }
}
