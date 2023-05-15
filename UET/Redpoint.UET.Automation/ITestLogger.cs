namespace Redpoint.UET.Automation
{
    public interface ITestLogger
    {
        void LogTrace(Worker? worker, string message);

        void LogInformation(Worker? worker, string message);

        void LogWarning(Worker? worker, string message);

        void LogError(Worker? worker, string message);

        void LogCrash(Worker? worker, string message);

        void LogCallstack(Worker? worker, string message);

        void LogStdout(Worker worker, string message);

        void LogStderr(Worker worker, string message);

        void LogException(Worker? worker, Exception exception, string context);

        void LogDiscovered(Worker? worker, TestResult testResult);

        void LogStarted(Worker? worker, TestResult testResult);

        void LogWaiting(Worker? worker, string message);

        void LogFinished(Worker? worker, TestResult testResult);
    }
}
