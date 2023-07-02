namespace Redpoint.ProcessExecution
{
    public class CaptureSpecificationDelegates
    {
        /// <summary>
        /// If this callback returns true, the line is emitted to the standard output.
        /// </summary>
        public required Func<string, bool> ReceiveStdout { get; init; }

        /// <summary>
        /// If this callback is not set, or it returns true, the line is emitted to standard error.
        /// </summary>
        public Func<string, bool>? ReceiveStderr { get; init; }
    }
}
