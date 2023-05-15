namespace Redpoint.ProcessExecution
{
    using System.Threading.Tasks;

    public interface IProcessExecutor
    {
        Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}
