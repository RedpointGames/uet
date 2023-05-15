namespace AutomationRunner
{
    public class TestResult
    {
        public string TestDisplayName { get; set; } = string.Empty;

        public string FullTestPath { get; set; } = string.Empty;

        public TestState State { get; set; } = TestState.NotRun;

        public string[] DeviceInstance { get; set; } = new string[0];

        public double Duration { get; set; }

        public string? DateTime { get; set; }

        public TestResultEntry[] Entries { get; set; } = new TestResultEntry[0];

        public int Warnings { get; set; }

        public int Errors { get; set; }

        public TestResultArtifact[] Artifacts { get; set; } = new TestResultArtifact[0];
    }
}
