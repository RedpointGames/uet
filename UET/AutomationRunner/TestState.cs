namespace AutomationRunner
{
    public enum TestState
    {
        Success,
        Fail,
        InProcess,
        NotRun,

        // Not a real state that can be in the JSON file, but synthesized by the
        // automation runner when we know a process crashed because of a test.
        Crash,
    }
}
