namespace Redpoint.ProcessExecution
{
    using System.Linq;
    using System.Threading.Tasks;

    internal class DefaultScriptExecutor : IScriptExecutor
    {
        private readonly IProcessExecutor _consoleExecutor;

        public DefaultScriptExecutor(IProcessExecutor consoleExecutor)
        {
            _consoleExecutor = consoleExecutor;
        }

        public async Task<int> ExecutePowerShellAsync(
            ScriptSpecification scriptSpecification,
            CancellationToken cancellationToken)
        {
            return await _consoleExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe",
                    Arguments = new[]
                    {
                        "-ExecutionPolicy",
                        "Bypass",
                        scriptSpecification.ScriptPath
                    }.Concat(scriptSpecification.Arguments),
                    EnvironmentVariables = scriptSpecification.EnvironmentVariables,
                },
                cancellationToken);
        }

        public async Task<int> CapturePowerShellAsync(
            ScriptSpecification scriptSpecification,
            CaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            return await _consoleExecutor.CaptureAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\WINDOWS\System32\WindowsPowerShell\v1.0\powershell.exe",
                    Arguments = new[]
                    {
                        "-ExecutionPolicy",
                        "Bypass",
                        scriptSpecification.ScriptPath
                    }.Concat(scriptSpecification.Arguments),
                    EnvironmentVariables = scriptSpecification.EnvironmentVariables,
                },
                captureSpecification,
                cancellationToken);
        }
    }
}
