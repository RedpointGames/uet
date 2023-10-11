namespace Redpoint.ProcessExecution
{
    using System.Threading.Tasks;

    /// <summary>
    /// Implementations of this interface can be registered in the <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>, and the <see cref="IProcessExecutor"/> implementation will call the methods before processes are executed.
    /// </summary>
    public interface IProcessExecutorHook
    {
        /// <summary>
        /// Allows the hook to modify the process specification before the process starts.
        /// </summary>
        /// <param name="processSpecification">The process specification that was passed to <see cref="IProcessExecutor"/>. It may also have been previously modified by another hook.</param>
        /// <param name="cancellationToken">The cancellation token that is cancelled when the process execution should be cancelled.</param>
        /// <returns>An asynchronous task.</returns>
        Task ModifyProcessSpecificationAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Allows the hook to modify the process specification before the process starts, and to perform cleanup operations when the process execution ends for any reason (either normal termination or cancellation) via the returned <see cref="IAsyncDisposable"/> object.
        /// </summary>
        /// <param name="processSpecification">The process specification that was passed to <see cref="IProcessExecutor"/>. It may also have been previously modified by another hook.</param>
        /// <param name="cancellationToken">The cancellation token that is cancelled when the process execution should be cancelled.</param>
        /// <returns>An asynchronous task that returns an optional <see cref="IAsyncDisposable"/>, which if provided will be disposed when the process execution ends for any reason.</returns>
        async Task<IAsyncDisposable?> ModifyProcessSpecificationWithCleanupAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken)
        {
            await ModifyProcessSpecificationAsync(processSpecification, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }
}
