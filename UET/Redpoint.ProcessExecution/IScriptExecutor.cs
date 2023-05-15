namespace Redpoint.ProcessExecution
{
    using System.Threading.Tasks;

    public interface IScriptExecutor
    {
        Task<int> ExecutePowerShellAsync(
            ScriptSpecification scriptSpecification,
            CancellationToken cancellationToken);

        Task<int> CapturePowerShellAsync(
            ScriptSpecification scriptSpecification,
            CaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}
