namespace Redpoint.ProcessExecution
{
    public class CaptureSpecification
    {
        public required Func<string, bool> ReceiveStdout { get; init; }

        public Func<string, bool>? ReceiveStderr { get; init; }
    }
}
