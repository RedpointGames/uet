namespace Redpoint.Uet.Automation.Model
{
    using System;

    public record class TestResult
    {
        public required string Platform { get; set; }

        public required string TestName { get; set; }

        public required string FullTestPath { get; set; }

        public required string? WorkerDisplayName { get; set; }

        public required TestResultStatus TestStatus { get; set; }

        public required DateTimeOffset DateStarted { get; set; }

        public required DateTimeOffset DateFinished { get; set; }

        public required TimeSpan Duration { get; set; }

        public required TestResultEntry[] Entries { get; set; }

        public string? EngineCrashInfo { get; set; }

        public Exception? AutomationRunnerCrashInfo { get; set; }

        public int AttemptCount { get; set; }
    }
}
