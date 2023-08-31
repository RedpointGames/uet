namespace Redpoint.ProcessExecution.Enumerable
{
    public sealed record class StandardOutputResponse : ProcessResponse
    {
        public required string Data { get; init; }
    }
}
