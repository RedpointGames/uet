namespace Redpoint.ProcessExecution
{
    using Redpoint.ProcessExecution.Enumerable;
    using System.Threading.Tasks;

    public interface IProcessExecutor
    {
        Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);

        IAsyncEnumerable<ProcessResponse> ExecuteAsync(
            ProcessSpecification processSpecification,
            CancellationToken cancellationToken);
    }
}
