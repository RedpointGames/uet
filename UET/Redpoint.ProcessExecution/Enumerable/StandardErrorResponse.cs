namespace Redpoint.ProcessExecution.Enumerable
{
    public sealed record class StandardErrorResponse : ProcessResponse
    {
        public required string Data { get; init; }
    }
}
