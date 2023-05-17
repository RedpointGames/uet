namespace Redpoint.UET.BuildPipeline.Executors
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.BuildGraph.Export;
    using Redpoint.UET.BuildPipeline.Executors.Engine;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class LocalBuildExecutor : IBuildExecutor
    {
        private readonly ILogger<LocalBuildExecutor> _logger;
        private readonly IBuildGraphExecutor _buildGraphGenerator;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;

        public LocalBuildExecutor(
            ILogger<LocalBuildExecutor> logger,
            IBuildGraphExecutor buildGraphGenerator,
            IEngineWorkspaceProvider engineWorkspaceProvider)
        {
            _logger = logger;
            _buildGraphGenerator = buildGraphGenerator;
            _engineWorkspaceProvider = engineWorkspaceProvider;
        }

        private class DAGNode
        {
            public required BuildGraphExportNode Node { get; set; }

            public required BuildGraphExportGroup Group { get; set; }

            public Lazy<Task<BuildResultStatus>>? ThisTask { get; set; }

            public DAGDependency[]? DependsTasks { get; set; }
        }

        private class DAGDependency
        {
            public required DAGNode Node { get; set; }
        }

        private async Task<BuildResultStatus> ExecuteDAGNode(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            DAGNode node,
            SemaphoreSlim? blockingSemaphore,
            CancellationToken cancellationToken)
        {
            if (node.DependsTasks != null)
            {
                var dependencyResults = await Task.WhenAll(node.DependsTasks.Select(x => x.Node.ThisTask!.Value));
                if (!dependencyResults.All(x => x == BuildResultStatus.Success))
                {
                    _logger.LogInformation($"Skipped: {node.Node.Name} = NotRun");
                    return BuildResultStatus.NotRun;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (blockingSemaphore != null)
            {
                await blockingSemaphore.WaitAsync(cancellationToken);
            }
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                await buildExecutionEvents.OnNodeStarted(node.Node.Name);
                try
                {
                    await using (var workspace = await _engineWorkspaceProvider.GetEngineWorkspace(buildSpecification.Engine, node.Node.Name, cancellationToken))
                    {
                        _logger.LogInformation($"Starting: {node.Node.Name}");
                        var exitCode = await _buildGraphGenerator.ExecuteGraphNodeAsync(
                            workspace.Path,
                            buildSpecification.BuildGraphScript,
                            buildSpecification.BuildGraphTarget,
                            node.Node.Name,
                            buildSpecification.BuildGraphLocalArtifactPath,
                            OperatingSystem.IsWindows() ? buildSpecification.BuildGraphSettings.WindowsSettings : buildSpecification.BuildGraphSettings.MacSettings,
                            buildSpecification.BuildGraphSettingReplacements,
                            CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                            {
                                ReceiveStdout = (line) =>
                                {
                                    buildExecutionEvents.OnNodeOutputReceived(node.Node.Name, new[] { line });
                                    return false;
                                },
                                ReceiveStderr = (line) =>
                                {
                                    buildExecutionEvents.OnNodeOutputReceived(node.Node.Name, new[] { line });
                                    return false;
                                },
                            }),
                            cancellationToken);
                        if (exitCode == 0)
                        {
                            _logger.LogInformation($"Finished: {node.Node.Name} = Success");
                            await buildExecutionEvents.OnNodeFinished(node.Node.Name, BuildResultStatus.Success);
                            return BuildResultStatus.Success;
                        }
                        else
                        {
                            _logger.LogError($"Finished: {node.Node.Name} = Failed");
                            await buildExecutionEvents.OnNodeFinished(node.Node.Name, BuildResultStatus.Failed);
                            return BuildResultStatus.Failed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Internal exception while running build job {node.Node.Name}: {ex.Message}");
                    await buildExecutionEvents.OnNodeFinished(node.Node.Name, BuildResultStatus.Failed);
                    return BuildResultStatus.Failed;
                }
            }
            finally
            {
                if (blockingSemaphore != null)
                {
                    blockingSemaphore.Release();
                }
            }
        }

        public async Task<int> ExecuteBuildAsync(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            ICaptureSpecification generationCaptureSpecification,
            CancellationToken cancellationToken)
        {
            BuildGraphExport buildGraph;
            await using (var workspace = await _engineWorkspaceProvider.GetEngineWorkspace(buildSpecification.Engine, "Generate BuildGraph JSON", cancellationToken))
            {
                _logger.LogInformation("Generating BuildGraph JSON based on settings...");
                buildGraph = await _buildGraphGenerator.GenerateGraphAsync(
                    workspace.Path,
                    buildSpecification.BuildGraphScript,
                    buildSpecification.BuildGraphTarget,
                    OperatingSystem.IsWindows() ? buildSpecification.BuildGraphSettings.WindowsSettings : buildSpecification.BuildGraphSettings.MacSettings,
                    buildSpecification.BuildGraphSettingReplacements,
                    generationCaptureSpecification,
                    cancellationToken);
            }

            _logger.LogInformation("Executing build...");

            SemaphoreSlim? blockingSemaphore = null;
            if (!buildSpecification.Engine._permitConcurrentBuilds)
            {
                blockingSemaphore = new SemaphoreSlim(1);
            }

            // Compute the DAG and execute tasks.
            var dag = new Dictionary<string, DAGNode>();
            foreach (var group in buildGraph.Groups)
            {
                foreach (var node in group.Nodes)
                {
                    dag.Add(node.Name, new DAGNode { Node = node, Group = group });
                }
            }
            var remainingNodes = new HashSet<DAGNode>(dag.Values);
            var allTasks = new List<Lazy<Task<BuildResultStatus>>>();
            while (remainingNodes.Count > 0)
            {
                foreach (var node in remainingNodes.ToArray())
                {
                    var dependsOn = node.Node.DependsOn.Split(";", StringSplitOptions.RemoveEmptyEntries);
                    var allowInitialize = true;
                    if (dependsOn.Length > 0)
                    {
                        foreach (var depend in dependsOn)
                        {
                            if (dag[depend].ThisTask == null)
                            {
                                allowInitialize = false;
                                break;
                            }
                        }
                        if (!allowInitialize)
                        {
                            continue;
                        }

                        node.DependsTasks = dependsOn.Select(x => new DAGDependency { Node = dag[x] }).ToArray();
                    }
                    if (!allowInitialize)
                    {
                        continue;
                    }
                    var nodeCopy = node;
                    var task = new Lazy<Task<BuildResultStatus>>(Task.Run(async () =>
                    {
                        return await ExecuteDAGNode(buildSpecification, buildExecutionEvents, nodeCopy, blockingSemaphore, cancellationToken);
                    }));
                    allTasks.Add(task);
                    node.ThisTask = task;
                    remainingNodes.Remove(node);
                }
            }

            _logger.LogInformation("Generated DAG information:");
            foreach (var kv in dag)
            {
                if (kv.Value.DependsTasks != null)
                {
                    _logger.LogInformation($"{kv.Key} = {string.Join(", ", kv.Value.DependsTasks.Select(x => x.Node.Node.Name))}");
                }
                else
                {
                    _logger.LogInformation($"{kv.Key} = (none)");
                }
            }

            var allResults = await Task.WhenAll(allTasks.Select(x => x.Value));

            return allResults.All(x => x == BuildResultStatus.Success) ? 0 : 1;
        }
    }
}
