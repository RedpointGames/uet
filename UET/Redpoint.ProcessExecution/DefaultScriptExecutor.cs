namespace Redpoint.ProcessExecution
{
    using Redpoint.PathResolution;
    using System.Linq;
    using System.Threading.Tasks;

    internal class DefaultScriptExecutor : IScriptExecutor
    {
        private readonly IProcessExecutor _consoleExecutor;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessArgumentParser _processArgumentParser;

        public DefaultScriptExecutor(
            IProcessExecutor consoleExecutor,
            IPathResolver pathResolver,
            IProcessArgumentParser processArgumentParser)
        {
            _consoleExecutor = consoleExecutor;
            _pathResolver = pathResolver;
            _processArgumentParser = processArgumentParser;
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
                            : "pwsh").ConfigureAwait(false),
                    Arguments = new[]
                    {
                        _processArgumentParser.CreateArgumentFromLogicalValue("-ExecutionPolicy"),
                        _processArgumentParser.CreateArgumentFromLogicalValue("Bypass"),
                        _processArgumentParser.CreateArgumentFromLogicalValue(scriptSpecification.ScriptPath)
                    }.Concat(scriptSpecification.Arguments),
                    EnvironmentVariables = scriptSpecification.EnvironmentVariables,
                },
                captureSpecification,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
