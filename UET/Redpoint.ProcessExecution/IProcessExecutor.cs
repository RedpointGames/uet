namespace Redpoint.ProcessExecution
{
    using System.Threading.Tasks;

    public interface IProcessExecutor
    {
        Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            CancellationToken cancellationToken);

        Task<int> CaptureAsync(
            ProcessSpecification processSpecification,
            CaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}
