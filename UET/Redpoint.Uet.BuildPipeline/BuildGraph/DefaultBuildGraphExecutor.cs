namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Export;
    using Redpoint.Uet.BuildPipeline.BuildGraph.MobileProvisioning;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Patching;
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Uat;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class DefaultBuildGraphExecutor : IBuildGraphExecutor
    {
        private readonly ILogger<DefaultBuildGraphExecutor> _logger;
        private readonly IUATExecutor _uatExecutor;
        private readonly IBuildGraphArgumentGenerator _buildGraphArgumentGenerator;
        private readonly IBuildGraphPatcher _buildGraphPatcher;
        private readonly IDynamicWorkspaceProvider _dynamicWorkspaceProvider;
        private readonly IMobileProvisioning _mobileProvisioning;

        public DefaultBuildGraphExecutor(
            ILogger<DefaultBuildGraphExecutor> logger,
            IUATExecutor uatExecutor,
            IBuildGraphArgumentGenerator buildGraphArgumentGenerator,
            IBuildGraphPatcher buildGraphPatcher,
            IDynamicWorkspaceProvider dynamicWorkspaceProvider,
            IMobileProvisioning mobileProvisioning)
        {
            _logger = logger;
            _uatExecutor = uatExecutor;
            _buildGraphArgumentGenerator = buildGraphArgumentGenerator;
            _buildGraphPatcher = buildGraphPatcher;
            _dynamicWorkspaceProvider = dynamicWorkspaceProvider;
            _mobileProvisioning = mobileProvisioning;
        }

        public async Task ListGraphAsync(
            string enginePath,
            BuildGraphScriptSpecification buildGraphScript,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            var exitCode = await InternalRunAsync(
                enginePath,
                string.Empty,
                string.Empty,
                string.Empty,
                buildGraphScript,
                string.Empty,
                string.Empty,
                new[] { $"-ListOnly" },
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    { "IsBuildMachine", "1" },
                    { "uebp_LOCAL_ROOT", enginePath },
                },
                null,
                captureSpecification,
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new BuildGraphExecutionFailure($"Failed to list options from build graph; UAT exited with non-zero exit code {exitCode}.");
            }
        }

        public async Task<int> ExecuteGraphNodeAsync(
            string enginePath,
            string buildGraphRepositoryRootPath,
            string uetPath,
            string artifactExportPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphNodeName,
            string buildGraphSharedStorageDir,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            Dictionary<string, string> globalEnvironmentVariables,
            IReadOnlyList<BuildConfigMobileProvision> mobileProvisions,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            await using ((await _dynamicWorkspaceProvider.GetWorkspaceAsync(new TemporaryWorkspaceDescriptor
            {
                Name = "NuGetPackages"
            }, cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var nugetPackages).ConfigureAwait(false))
            {
                var environmentVariables = new Dictionary<string, string>
                {
                    { "IsBuildMachine", "1" },
                    { "uebp_LOCAL_ROOT", enginePath },
                    // BuildGraph in Unreal Engine 5.0 causes input files to be unnecessarily modified. Just allow mutation since I'm not sure what the bug is.
                    { "BUILD_GRAPH_ALLOW_MUTATION", "true" },
                    // Make sure UET knows it's running under BuildGraph for subcommands
                    // so that we can emit the extra newline necessary for BuildGraph to
                    // show all output. Refer to CommandExtensions.cs to see where this
                    // is used.
                    { "UET_RUNNING_UNDER_BUILDGRAPH", "true" },
                    { "UET_XGE_SHIM_BUILD_NODE_NAME", buildGraphNodeName },
                    // Isolate NuGet package restore so that multiple jobs can restore at
                    // the same time.
                    { "NUGET_PACKAGES", nugetPackages.Path }
                };
                if (!string.IsNullOrWhiteSpace(buildGraphRepositoryRootPath))
                {
                    environmentVariables["BUILD_GRAPH_PROJECT_ROOT"] = buildGraphRepositoryRootPath;
                }
                else
                {
                    environmentVariables["BUILD_GRAPH_PROJECT_ROOT"] = enginePath;
                }
                if (string.IsNullOrWhiteSpace(environmentVariables["BUILD_GRAPH_PROJECT_ROOT"]))
                {
                    throw new InvalidOperationException("BUILD_GRAPH_PROJECT_ROOT is empty, when it should be set to either the repository root or engine path.");
                }
                else
                {
                    _logger.LogInformation($"BuildGraph is executing with BUILD_GRAPH_PROJECT_ROOT={environmentVariables["BUILD_GRAPH_PROJECT_ROOT"]}");
                }
                foreach (var kv in globalEnvironmentVariables)
                {
                    environmentVariables[kv.Key] = kv.Value;
                }

                return await InternalRunAsync(
                    enginePath,
                    buildGraphRepositoryRootPath,
                    uetPath,
                    artifactExportPath,
                    buildGraphScript,
                    buildGraphTarget,
                    buildGraphSharedStorageDir,
                    new[]
                    {
                        $"-SingleNode={buildGraphNodeName}",
                        "-WriteToSharedStorage",
                        $"-SharedStorageDir={buildGraphSharedStorageDir}"
                    },
                    buildGraphArguments,
                    buildGraphArgumentReplacements,
                    environmentVariables,
                    mobileProvisions,
                    captureSpecification,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<BuildGraphExport> GenerateGraphAsync(
            string enginePath,
            string buildGraphRepositoryRootPath,
            string uetPath,
            string artifactExportPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphSharedStorageDir,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            var buildGraphOutput = Path.GetTempFileName();
            var deleteBuildGraphOutput = true;
            try
            {
                var exitCode = await InternalRunAsync(
                    enginePath,
                    buildGraphRepositoryRootPath,
                    uetPath,
                    artifactExportPath,
                    buildGraphScript,
                    buildGraphTarget,
                    buildGraphSharedStorageDir,
                    new[] { $"-Export={buildGraphOutput}" },
                    buildGraphArguments,
                    buildGraphArgumentReplacements,
                    new Dictionary<string, string>
                    {
                        { "IsBuildMachine", "1" },
                        { "uebp_LOCAL_ROOT", enginePath },
                    },
                    null,
                    captureSpecification,
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new BuildGraphExecutionFailure($"Failed to generate build graph; UAT exited with non-zero exit code {exitCode}.");
                }

                using (var reader = new FileStream(buildGraphOutput, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try
                    {
                        var json = JsonSerializer.Deserialize<BuildGraphExport>(reader, BuildGraphSourceGenerationContext.Default.BuildGraphExport);
                        if (json == null)
                        {
                            deleteBuildGraphOutput = false;
                            throw new BuildGraphExecutionFailure($"Failed to generate build graph; UAT did not produce a valid BuildGraph JSON file. Output file is stored at: '{buildGraphOutput}'.");
                        }
                        return json;
                    }
                    catch (JsonException ex)
                    {
                        deleteBuildGraphOutput = false;
                        throw new BuildGraphExecutionFailure($"Failed to generate build graph; UAT did not produce a valid BuildGraph JSON file. Output file is stored at: '{buildGraphOutput}'. Original exception was: {ex}");
                    }
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

        private async Task<int> InternalRunAsync(
            string enginePath,
            string buildGraphRepositoryRootPath,
            string uetPath,
            string artifactExportPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphSharedStorageDir,
            string[] internalArguments,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            Dictionary<string, string> buildGraphEnvironmentVariables,
            IReadOnlyList<BuildConfigMobileProvision>? mobileProvisions,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            string buildGraphScriptPath;
            var deleteBuildGraphScriptPath = false;
            if (buildGraphScript._forEngine)
            {
                buildGraphScriptPath = Path.Combine(
                    enginePath,
                    "Engine",
                    "Build",
                    "InstalledEngineBuild.xml");
            }
            else if (buildGraphScript._forPlugin)
            {
                buildGraphScriptPath = Path.GetTempFileName();
                using (var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.BuildPipeline.BuildGraph.BuildGraph_Plugin.xml"))
                {
                    using (var writer = new FileStream(buildGraphScriptPath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        await reader!.CopyToAsync(writer, cancellationToken).ConfigureAwait(false);
                    }
                }
                deleteBuildGraphScriptPath = true;
            }
            else if (buildGraphScript._forProject)
            {
                buildGraphScriptPath = Path.GetTempFileName();
                using (var reader = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.BuildPipeline.BuildGraph.BuildGraph_Project.xml"))
                {
                    using (var writer = new FileStream(buildGraphScriptPath, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        await reader!.CopyToAsync(writer, cancellationToken).ConfigureAwait(false);
                    }
                }
                deleteBuildGraphScriptPath = true;
            }
            else
            {
                throw new NotSupportedException();
            }

            await _buildGraphPatcher.PatchBuildGraphAsync(enginePath, buildGraphScript._forEngine).ConfigureAwait(false);

            if (mobileProvisions != null)
            {
                await _mobileProvisioning.InstallMobileProvisions(enginePath, buildGraphScript._forEngine, mobileProvisions, cancellationToken).ConfigureAwait(false);
            }

            if (buildGraphEnvironmentVariables.Count == 0)
            {
                _logger.LogTrace("Executing BuildGraph with no environment variables.");
            }
            else
            {
                _logger.LogTrace($"Executing BuildGraph with the following environment variables:");
                foreach (var kv in buildGraphEnvironmentVariables)
                {
                    _logger.LogTrace($"  {kv.Key}={kv.Value}");
                }
            }

            try
            {
                return await _uatExecutor.ExecuteAsync(
                    enginePath,
                    new UATSpecification
                    {
                        Command = "BuildGraph",
                        Arguments = Array.Empty<string>()
                            .Concat(string.IsNullOrWhiteSpace(buildGraphTarget) ? Array.Empty<string>() : new[] { $"-Target={buildGraphTarget}" })
                            .Concat(new[]
                            {
                                "-noP4",
                            })
                            .Concat(string.IsNullOrWhiteSpace(buildGraphScriptPath) ? Array.Empty<string>() : new[] { $"-Script={buildGraphScriptPath}" })
                            .Concat(internalArguments)
                            .Concat(_buildGraphArgumentGenerator.GenerateBuildGraphArguments(
                                buildGraphArguments,
                                buildGraphArgumentReplacements,
                                buildGraphRepositoryRootPath,
                                uetPath,
                                enginePath,
                                buildGraphSharedStorageDir,
                                artifactExportPath)),
                        EnvironmentVariables = buildGraphEnvironmentVariables
                    },
                    captureSpecification,
                    cancellationToken).ConfigureAwait(false);
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
