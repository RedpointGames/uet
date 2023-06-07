namespace Redpoint.UET.Automation.TestLogging
{
    using Redpoint.UET.Automation.Model;
    using Redpoint.UET.Automation.Worker;
    using System;

    internal class NullTestLogger : ITestLogger
    {
        public Task LogWorkerStarting(IWorker worker)
        {
            return Task.CompletedTask;
        }

        public Task LogWorkerStarted(IWorker worker, TimeSpan startupDuration)
        {
            return Task.CompletedTask;
        }

        public Task LogWorkerStopped(IWorker worker, IWorkerCrashData? workerCrashData)
        {
            return Task.CompletedTask;
        }

        public Task LogDiscovered(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            return Task.CompletedTask;
        }

        public Task LogStarted(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            return Task.CompletedTask;
        }

        public Task LogFinished(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult)
        {
            return Task.CompletedTask;
        }

        public Task LogException(IWorker worker, TestProgressionInfo progressionInfo, Exception exception, string context)
        {
            return Task.CompletedTask;
        }
    }
}
