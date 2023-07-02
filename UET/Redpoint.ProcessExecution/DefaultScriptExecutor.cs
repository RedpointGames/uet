namespace Redpoint.ProcessExecution
{
    using Redpoint.PathResolution;
    using System.Linq;
    using System.Threading.Tasks;

    internal class DefaultScriptExecutor : IScriptExecutor
    {
        private readonly IProcessExecutor _consoleExecutor;
        private readonly IPathResolver _pathResolver;

        public DefaultScriptExecutor(
            IProcessExecutor consoleExecutor,
            IPathResolver pathResolver)
        {
            _consoleExecutor = consoleExecutor;
            _pathResolver = pathResolver;
        }

        public async Task<int> ExecutePowerShellAsync(
            ScriptSpecification scriptSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            return await _consoleExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = await _pathResolver.ResolveBinaryPath(
                        OperatingSystem.IsWindows()
                            ? "powershell"
                            : "pwsh"),
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
