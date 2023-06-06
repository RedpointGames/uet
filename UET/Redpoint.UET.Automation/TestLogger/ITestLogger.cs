namespace Redpoint.UET.Automation.TestLogging
{
    using Redpoint.UET.Automation.Model;
    using Redpoint.UET.Automation.Worker;

    public interface ITestLogger
    {
        void LogDiscovered(IWorker worker, TestResult testResult);

        void LogStarted(IWorker worker, TestResult testResult);

        void LogFinished(IWorker worker, TestResult testResult);

        void LogException(IWorker worker, Exception exception, string context);
    }
}