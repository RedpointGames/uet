namespace Redpoint.ProcessExecution.Enumerable
{
    public sealed record class ExitCodeResponse : ProcessResponse
    {
        public required int ExitCode { get; init; }
    }
}
