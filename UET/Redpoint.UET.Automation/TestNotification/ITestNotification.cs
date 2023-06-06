namespace Redpoint.UET.Automation.TestNotification
{
    using Redpoint.UET.Automation.Model;

    public interface ITestNotification
    {
        void TestDiscovered(TestResult testResult);

        void TestStarted(TestResult testResult);

        void TestFinished(TestResult testResult);

        Task FlushAsync();
    }
}
