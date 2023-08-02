namespace Redpoint.ProcessExecution.Enumerable
{
    internal sealed record class InternalExceptionResponse : ProcessResponse
    {
        public required Exception Exception { get; init; }
    }
}
