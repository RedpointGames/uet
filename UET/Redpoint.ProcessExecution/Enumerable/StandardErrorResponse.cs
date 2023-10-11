namespace Redpoint.ProcessExecution.Enumerable
{
    /// <summary>
    /// Yielded by the enumerator from <see cref="IProcessExecutor.ExecuteAsync(ProcessSpecification, CancellationToken)"/> when the process emits data to the standard error stream.
    /// </summary>
    public sealed record class StandardErrorResponse : ProcessResponse
    {
        /// <summary>
        /// The data emitted by the process to the standard error stream.
        /// </summary>
        public required string Data { get; init; }
    }
}
