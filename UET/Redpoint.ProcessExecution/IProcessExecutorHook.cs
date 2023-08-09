namespace Redpoint.ProcessExecution
{
    using System.Threading.Tasks;

    public interface IProcessExecutorHook
    {
        Task ModifyProcessSpecificationAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        async Task<IAsyncDisposable?> ModifyProcessSpecificationWithCleanupAsync(ProcessSpecification processSpecification, CancellationToken cancellationToken)
        {
            await ModifyProcessSpecificationAsync(processSpecification, cancellationToken);
            return null;
        }
    }
}
