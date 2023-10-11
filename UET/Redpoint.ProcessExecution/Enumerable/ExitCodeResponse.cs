namespace Redpoint.ProcessExecution.Enumerable
{
    /// <summary>
    /// Yielded by the enumerator from <see cref="IProcessExecutor.ExecuteAsync(ProcessSpecification, CancellationToken)"/> when the process exits.
    /// </summary>
    public sealed record class ExitCodeResponse : ProcessResponse
    {
        /// <summary>
        /// The exit code of the process.
        /// </summary>
        public required int ExitCode { get; init; }
    }
}
