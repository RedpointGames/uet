namespace Redpoint.UET.BuildPipeline.Executors.BuildServer
{
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph.Export;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.Executors.Engine;
    using Redpoint.UET.Workspace;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using System.Linq;

    public abstract class BuildServerBuildExecutor : IBuildExecutor
    {
        private readonly ILogger<BuildServerBuildExecutor> _logger;
        private readonly IBuildGraphExecutor _buildGraphExecutor;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
        private readonly IWorkspaceProvider _workspaceProvider;

        public BuildServerBuildExecutor(
            ILogger<BuildServerBuildExecutor> logger,
            IBuildGraphExecutor buildGraphExecutor,
            IEngineWorkspaceProvider engineWorkspaceProvider,
            IWorkspaceProvider workspaceProvider)
        {
            _logger = logger;
            _buildGraphExecutor = buildGraphExecutor;
            _engineWorkspaceProvider = engineWorkspaceProvider;
            _workspaceProvider = workspaceProvider;
        }

        public virtual async Task<int> ExecuteBuildAsync(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            ICaptureSpecification generationCaptureSpecification,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(buildSpecification.BuildServerOutputFilePath))
            {
                throw new BuildPipelineExecutionFailure("This build executor requires BuildServerOutputFilePath to be set.");
            }

            BuildGraphExport buildGraph;
            await using (var engineWorkspace = await _engineWorkspaceProvider.GetEngineWorkspace(
                buildSpecification.Engine,
                string.Empty,
                buildSpecification.UseStorageVirtualisation,
                cancellationToken))
            {
                // @note: Generating the BuildGraph doesn't require any files from the workspace, so we don't bother
                // setting up a Git workspace for it.
                await using (var temporaryWorkspace = await _workspaceProvider.GetTempWorkspaceAsync("Generate BuildGraph JSON"))
                {
                    _logger.LogInformation("Generating BuildGraph JSON based on settings...");
                    buildGraph = await _buildGraphExecutor.GenerateGraphAsync(
                        engineWorkspace.Path,
                        temporaryWorkspace.Path,
                        buildSpecification.BuildGraphScript,
                        buildSpecification.BuildGraphTarget,
                        OperatingSystem.IsWindows() ? buildSpecification.BuildGraphSettings.WindowsSettings : buildSpecification.BuildGraphSettings.MacSettings,
                        buildSpecification.BuildGraphSettingReplacements,
                        generationCaptureSpecification,
                        cancellationToken);
                }
            }

            var agentTypeMapping = new Dictionary<string, BuildServerJobPlatform>
            {
                { "Win64", BuildServerJobPlatform.Windows },
                { "Win64_Licensee", BuildServerJobPlatform.Windows },
                { "HoloLens", BuildServerJobPlatform.Windows },
                { "Mac", BuildServerJobPlatform.Mac },
                { "Mac_Licensee", BuildServerJobPlatform.Mac },
                { "Meta", BuildServerJobPlatform.Meta },
            };

            var nodeMap = GetNodeMap(buildGraph);

            var pipeline = new BuildServerPipeline();

            // @note: We need the distribution information here for this to work.
            /*
            if (@todo: is plugin?)
            {
                pipeline.GlobalEnvironmentVariables.Add("BUILDING_FOR_DISTRIBUTION", "true");
            }
            foreach (var kv in distribution.EnvironmentVariables)
            {
                pipeline.GlobalEnvironmentVariables.Add(kv.Key, kv.Value);
            }
            */

            foreach (var group in buildGraph.Groups)
            {
                if (group.AgentTypes.Length == 0 ||
                    !agentTypeMapping.ContainsKey(group.AgentTypes[0]))
                {
                    throw new NotSupportedException($"Unknown AgentType specified in BuildGraph: {string.Join(",", group.AgentTypes)}");
                }

                foreach (var node in group.Nodes)
                {
                    var needs = new HashSet<string>();
                    GetFullDependenciesOfNode(nodeMap, node, needs);

                    var job = new BuildServerJob
                    {
                        Name = node.Name,
                        Stage = group.Name,
                        Needs = needs.ToArray(),
                        Platform = agentTypeMapping[group.AgentTypes[0]],
                        IsManual = node.Name.StartsWith("Deploy Manual "),
                    };

                    if (node.Name.StartsWith("Automation "))
                    {
                        job.ArtifactPaths = new[]
                        {
                            // @note: These are out of date because UET won't save automation output to these paths.
                            "BuildScripts/Temp/*/TestResults_*.xml",
                            "BuildScripts/Temp/T*/Saved/Logs/Worker*.log",
                        };
                        job.ArtifactJUnitReportPath = "BuildScripts/Temp/*/TestResults_*.xml";
                    }

                    // @todo: We need to emit scripts for each platform
                    // that will fetch and download *the current* UET
                    // that is executing to a known path and then execute
                    // it.
                    //
                    // I'm not sure of the best way to tackle this yet.
                    // Some ideas:
                    //  - UET version in BuildConfig.json, pull that out
                    //    and use that to download/launch the real UET
                    //    from C:\ProgramData\UET\<commit hash>
                    //  - Share current executable via build artifacts?
                    //    How would this work for multi-platform though?

                    switch (job.Platform)
                    {
                        case BuildServerJobPlatform.Windows:

                            break;
                        case BuildServerJobPlatform.Mac:
                            break;
                        case BuildServerJobPlatform.Meta:
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            await EmitBuildServerSpecificFileAsync(
                buildSpecification,
                pipeline,
                buildSpecification.BuildServerOutputFilePath);
            return 0;
        }

        private Dictionary<string, BuildGraphExportNode> GetNodeMap(BuildGraphExport buildGraph)
        {
            return buildGraph.Groups.SelectMany(x => x.Nodes)
                .ToDictionary(k => k.Name, v => v);
        }

        private void GetFullDependenciesOfNode(
            Dictionary<string, BuildGraphExportNode> nodeMap,
            BuildGraphExportNode node,
            HashSet<string> allDependencies)
        {
            foreach (var dependency in node.DependsOn.Split(';'))
            {
                if (nodeMap.ContainsKey(dependency))
                {
                    allDependencies.Add(dependency);
                    GetFullDependenciesOfNode(nodeMap, nodeMap[dependency], allDependencies);
                }
            }
        }

        protected abstract Task EmitBuildServerSpecificFileAsync(
            BuildSpecification buildSpecification,
            BuildServerPipeline buildServerPipeline,
            string buildServerOutputFilePath);
    }
}