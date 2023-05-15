namespace Redpoint.ProcessExecution
{
    using System.Threading.Tasks;

    public interface IScriptExecutor
    {
        Task<int> ExecutePowerShellAsync(
            ScriptSpecification scriptSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}
