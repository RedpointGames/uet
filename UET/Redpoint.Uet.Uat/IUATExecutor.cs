namespace Redpoint.Uet.Uat
{
    using Redpoint.ProcessExecution;

    public interface IUATExecutor
    {
        Task<int> ExecuteAsync(
            string enginePath,
            UATSpecification uatSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}