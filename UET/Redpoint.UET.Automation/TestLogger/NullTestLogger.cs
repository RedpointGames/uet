namespace Redpoint.UET.Automation.TestLogging
{
    using Redpoint.UET.Automation.Model;
    using Redpoint.UET.Automation.Worker;
    using System;

    internal class NullTestLogger : ITestLogger
    {
        public void LogCallstack(IWorker worker, string message)
        {
        }

        public void LogCrash(IWorker worker, string message)
        {
        }

        public void LogDiscovered(IWorker worker, TestResult testResult)
        {
        }

        public void LogError(IWorker worker, string message)
        {
        }

        public void LogException(IWorker worker, Exception exception, string context)
        {
        }

        public void LogFinished(IWorker worker, TestResult testResult)
        {
        }

        public void LogInformation(IWorker worker, string message)
        {
        }

        public void LogStarted(IWorker worker, TestResult testResult)
        {
        }

        public void LogStderr(IWorker IWorker, string message)
        {
        }

        public void LogStdout(IWorker IWorker, string message)
        {
        }

        public void LogTrace(IWorker worker, string message)
        {
        }

        public void LogWaiting(IWorker worker, string message)
        {
        }

        public void LogWarning(IWorker worker, string message)
        {
        }
    }
}
