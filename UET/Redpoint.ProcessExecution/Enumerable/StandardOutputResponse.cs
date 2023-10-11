namespace Redpoint.ProcessExecution.Enumerable
{
    /// <summary>
    /// Yielded by the enumerator from <see cref="IProcessExecutor.ExecuteAsync(ProcessSpecification, CancellationToken)"/> when the process emits data to the standard output stream.
    /// </summary>
    public sealed record class StandardOutputResponse : ProcessResponse
    {
        /// <summary>
        /// The data emitted by the process to the standard output stream.
        /// </summary>
        public required string Data { get; init; }
    }
}
