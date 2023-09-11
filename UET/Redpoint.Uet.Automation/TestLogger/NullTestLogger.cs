namespace Redpoint.Uet.Automation.TestLogging
{
    using Redpoint.Uet.Automation.Model;
    using Redpoint.Uet.Automation.Worker;
    using System;

    internal sealed class NullTestLogger : ITestLogger
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

        public Task LogTestRunTimedOut(TimeSpan testRunTimeout)
        {
            return Task.CompletedTask;
        }
    }
}
