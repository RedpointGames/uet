namespace AutomationRunner
{
    public class TestResultEntry
    {
        public TestResultEntryEvent Event { get; set; } = new TestResultEntryEvent();

        public string Filename { get; set; } = string.Empty;

        public int LineNumber { get; set; }

        public string Timestamp { get; set; } = string.Empty;
    }
}
