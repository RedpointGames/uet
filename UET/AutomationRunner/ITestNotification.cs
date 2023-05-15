namespace AutomationRunner
{
    public interface ITestNotification
    {
        CancellationTokenSource CancellationTokenSource { get; }

        void TestDiscovered(TestResult testResult);

        void TestStarted(TestResult testResult);

        void TestFinished(TestResult testResult);

        Task WaitAsync();
    }
}
