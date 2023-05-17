namespace Redpoint.UET.BuildPipeline.BuildGraph
{
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph.Export;
    using Redpoint.UET.UAT;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class DefaultBuildGraphGenerator : IBuildGraphGenerator
    {
        private readonly IUATExecutor _uatExecutor;

        public DefaultBuildGraphGenerator(
            IUATExecutor uatExecutor)
        {
            _uatExecutor = uatExecutor;
        }

        public async Task<BuildGraphExport> GenerateGraphAsync(
            string enginePath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            IEnumerable<string> buildGraphArguments,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            string buildGraphScriptPath;
            var deleteBuildGraphScriptPath = false;
            if (buildGraphScript._path != null)
            {
                buildGraphScriptPath = buildGraphScript._path;
            }
            else if (buildGraphScript._forPlugin)
            {
                buildGraphScriptPath = Path.GetTempFileName();
                using (var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.UET.BuildPipeline.BuildGraph.BuildGraph_Plugin.xml"))
                {
                    using (var writer = new FileStream(buildGraphScriptPath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        await reader!.CopyToAsync(writer, cancellationToken);
                    }
                }
                deleteBuildGraphScriptPath = true;
            }
            else if (buildGraphScript._forProject)
            {
                buildGraphScriptPath = Path.GetTempFileName();
                using (var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.UET.BuildPipeline.BuildGraph.BuildGraph_Project.xml"))
                {
                    using (var writer = new FileStream(buildGraphScriptPath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        await reader!.CopyToAsync(writer, cancellationToken);
                    }
                }
                deleteBuildGraphScriptPath = true;
            }
            else
            {
                throw new NotSupportedException();
            }

            try
            {
                var buildGraphOutput = Path.GetTempFileName();
                var deleteBuildGraphOutput = true;
                try
                {
                    var exitCode = await _uatExecutor.ExecuteAsync(
                        enginePath,
                        new UATSpecification
                        {
                            Command = "BuildGraph",
                            Arguments = new[]
                            {
                                $"-Target={buildGraphTarget}",
                                "-noP4",
                                $"-Export={buildGraphOutput}",
                                $"-Script={buildGraphScriptPath}",
                            }.Concat(buildGraphArguments),
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "IsBuildMachine", "1" },
                                { "uebp_LOCAL_ROOT", enginePath },
                            }
                        },
                        captureSpecification,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        throw new BuildGraphExecutionFailure($"Failed to generate build graph; UAT exited with exit code {exitCode}.");
                    }

                    using (var reader = new FileStream(buildGraphOutput, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        var json = JsonSerializer.Deserialize<BuildGraphExport>(reader);
                        if (json == null)
                        {
                            deleteBuildGraphOutput = false;
                            throw new BuildGraphExecutionFailure($"Failed to generate build graph; UAT did not produce a valid BuildGraph JSON file. Output file is stored at: {buildGraphOutput}");
                        }
                        return json;
                    }
                }
                finally
                {
                    if (deleteBuildGraphOutput)
                    {
                        File.Delete(buildGraphOutput);
                    }
                }
            }
            finally
            {
                if (deleteBuildGraphScriptPath)
                {
                    File.Delete(buildGraphScriptPath);
                }
            }
        }
    }
}
