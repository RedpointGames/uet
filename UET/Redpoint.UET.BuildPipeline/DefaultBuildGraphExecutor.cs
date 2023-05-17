namespace Redpoint.UET.BuildPipeline
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.Core;
    using System.Threading.Tasks;

    internal class DefaultBuildGraphExecutor : IBuildGraphExecutor
    {
        private readonly IScriptExecutor _scriptExecutor;
        private readonly IPathProvider _pathProvider;
        private readonly ILogger<DefaultBuildGraphExecutor> _logger;

        public DefaultBuildGraphExecutor(
            IScriptExecutor scriptExecutor,
            IPathProvider pathProvider,
            ILogger<DefaultBuildGraphExecutor> logger)
        {
            _scriptExecutor = scriptExecutor;
            _pathProvider = pathProvider;
            _logger = logger;
        }

        public async Task<int> ExecuteGraphAsync(
            string enginePath,
            string buildGraphScriptPath,
            string buildGraphTarget,
            IEnumerable<string> buildGraphArguments,
            CancellationToken cancellationToken)
        {
            var buildLog = Path.Combine(_pathProvider.BuildScriptsTemp, "Build.log");

            while (true)
            {
                if (File.Exists(buildLog))
                {
                    File.Delete(buildLog);
                }
                int exitCode;
                var forceFail = false;
                using (var writer = new StreamWriter(buildLog))
                {
                    exitCode = await _scriptExecutor.ExecutePowerShellAsync(
                        new ScriptSpecification
                        {
                            ScriptPath = Path.Combine(_pathProvider.BuildScriptsLib, "Internal_RunUAT.ps1"),
                            Arguments = new[]
                            {
                                "-UATEnginePath",
                                enginePath,
                                "BuildGraph",
                                $@"-Script=""{buildGraphScriptPath}""",
                                $@"-Target=""{buildGraphTarget}""",
                            }.Concat(buildGraphArguments),
                        },
                        CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                        {
                            ReceiveStdout = line =>
                            {
                                writer.WriteLine(line);
                                if (line.StartsWith("Unhandled exception:"))
                                {
                                    forceFail = true;
                                }
                                return true;
                            },
                        }),
                        cancellationToken);
                }
                if (forceFail)
                {
                    _logger.LogError("One or more errors were detected via the output log. Forcing a failure.");
                    return exitCode == 0 ? 1 : exitCode;
                }
                if (exitCode != 0)
                {
                    var pchRetryOn = true;
                    var needsRetry = false;
                    foreach (var line in File.ReadAllLines(buildLog))
                    {
                        if (line.Contains("error C3859") && pchRetryOn)
                        {
                            needsRetry = true;
                            break;
                        }
                        if (line.Contains("@buildgraph PCH-RETRY-ON"))
                        {
                            pchRetryOn = true;
                        }
                        if (line.Contains("@buildgraph PCH-RETRY-OFF"))
                        {
                            pchRetryOn = false;
                        }
                    }
                    if (needsRetry)
                    {
                        _logger.LogWarning("Detected PCH memory error. Automatically retrying...");
                        continue;
                    }
                }
                return exitCode;
            }
        }
    }
}
