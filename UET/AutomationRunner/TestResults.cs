namespace AutomationRunner
{
    using System.Collections.ObjectModel;

    public class TestResults
    {
        public string ClientDescriptor { get; set; } = string.Empty;

        public double TotalDuration { get; set; }

        public List<TestResult> Results { get; set; } = new List<TestResult>();

        /// <summary>
        /// Tests are unsuccessful if there are any failing tests, or there are no test results at all.
        /// </summary>
        public bool AnyRetryableFailures => Results.Count == 0 || Results.Any(x => x.State != TestState.Success);

        /// <summary>
        /// If the overall result is failure.
        /// </summary>
        public bool AnyFailures => AnyRetryableFailures || WorkerCrashes.Count > 0;

        public List<(Worker worker, string[] crashLogs)> WorkerCrashes { get; set; } = new List<(Worker worker, string[] crashLogs)>();
    }
}
