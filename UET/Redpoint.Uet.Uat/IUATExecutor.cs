namespace Redpoint.Uet.Uat
{
    using Redpoint.ProcessExecution;

    public interface IUATExecutor
    {
        Task<int> ExecuteAsync(
            string enginePath,
            UATSpecification uatSpecification,
            ICaptureSpecification captureSpecification,
            string[] forceRetryMessages,
            CancellationToken cancellationToken);
    }
}