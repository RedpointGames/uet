namespace Redpoint.ProcessExecution
{
    using System.Threading.Tasks;

    /// <summary>
    /// The script executor interface, which provides simplified APIs for executing PowerShell scripts.
    /// </summary>
    public interface IScriptExecutor
    {
        /// <summary>
        /// Executes the PowerShell script specified by <paramref name="scriptSpecification"/> and returns asynchronously once the script exits. This will use powershell.exe on Windows and pwsh on all other platforms.
        /// </summary>
        /// <param name="scriptSpecification">Specifies which PowerShell script should be started and how it should be executed.</param>
        /// <param name="captureSpecification">Specifies how the output of the script should be captured.</param>
        /// <param name="cancellationToken">A cancellation token which can be used to terminate the script. When this cancellation token is cancelled, <see cref="OperationCanceledException"/> is thrown as this function can not return an exit code.</param>
        /// <returns>The exit code of the process.</returns>
        Task<int> ExecutePowerShellAsync(
            ScriptSpecification scriptSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}
