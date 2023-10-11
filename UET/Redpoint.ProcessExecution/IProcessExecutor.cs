namespace Redpoint.ProcessExecution
{
    using Redpoint.ProcessExecution.Enumerable;
    using System.Threading.Tasks;

    /// <summary>
    /// The process executor service, which provides APIs for starting and running processes on the local machine.
    /// </summary>
    public interface IProcessExecutor
    {
        /// <summary>
        /// Executes the process specified by <paramref name="processSpecification"/> and returns asynchronously once the process exits.
        /// </summary>
        /// <param name="processSpecification">Specifies which executable binary should be started and how it should be executed.</param>
        /// <param name="captureSpecification">Specifies how the output of the process should be captured.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to terminate the process. When this cancellation token is cancelled, <see cref="OperationCanceledException"/> is thrown as this function can not return an exit code.</param>
        /// <returns>The exit code of the process.</returns>
        /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled and the process terminated.</exception>
        Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);

        /// <summary>
        /// Executes the process specified by <paramref name="processSpecification"/> and returns an asynchronous enumerable of process responses. Each process response represents data from standard output or standard error, or represents the process terminating. Each yielded value will be one of <see cref="StandardOutputResponse"/>, <see cref="StandardErrorResponse"/> or <see cref="ExitCodeResponse"/>. <see cref="ExitCodeResponse"/> is always the last response returned before the enumerable stops yielding more values.
        /// </summary>
        /// <param name="processSpecification">Specifies which executable binary should be started and how it should be executed.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to terminate the process. When this cancellation token is cancelled, the process is terminated and the returned enumerable stops yielding values.</param>
        /// <returns>An asynchronous stream of standard output and standard error data, followed by the exit code response.</returns>
        IAsyncEnumerable<ProcessResponse> ExecuteAsync(
            ProcessSpecification processSpecification,
            CancellationToken cancellationToken);
    }
}
