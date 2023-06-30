namespace Redpoint.Uet.Automation.TestLogging
{
    using Redpoint.Uet.Automation.Model;
    using Redpoint.Uet.Automation.Worker;

    public record class TestProgressionInfo
    {
        public int TestsRemaining { get; set; }

        public int TestsTotal { get; set; }
    }

    public interface ITestLogger
    {
        Task LogWorkerStarting(IWorker worker);

        Task LogWorkerStarted(IWorker worker, TimeSpan startupDuration);

        Task LogWorkerStopped(IWorker worker, IWorkerCrashData? workerCrashData);

        Task LogDiscovered(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult);

        Task LogStarted(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult);

        Task LogFinished(IWorker worker, TestProgressionInfo progressionInfo, TestResult testResult);

        Task LogException(IWorker worker, TestProgressionInfo progressionInfo, Exception exception, string context);

        Task LogTestRunTimedOut(TimeSpan testRunTimeout);
    }
}