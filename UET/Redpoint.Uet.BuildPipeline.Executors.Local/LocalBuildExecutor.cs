namespace Redpoint.Uet.BuildPipeline.Executors.Local
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Export;
    using Redpoint.Uet.BuildPipeline.BuildGraph.PreBuild;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class LocalBuildExecutor : IBuildExecutor
    {
        private readonly ILogger<LocalBuildExecutor> _logger;
        private readonly IBuildGraphExecutor _buildGraphExecutor;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
        private readonly IDynamicWorkspaceProvider _workspaceProvider;
        private readonly ISdkSetupForBuildExecutor _sdkSetupForBuildExecutor;
        private readonly IBuildGraphArgumentGenerator _buildGraphArgumentGenerator;
        private readonly IDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider>[] _pluginPrepare;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>[] _projectPrepare;
        private readonly IPreBuild _preBuild;

        public LocalBuildExecutor(
            IServiceProvider serviceProvider,
            ILogger<LocalBuildExecutor> logger,
            IBuildGraphExecutor buildGraphGenerator,
            IEngineWorkspaceProvider engineWorkspaceProvider,
            IDynamicWorkspaceProvider workspaceProvider,
            ISdkSetupForBuildExecutor sdkSetupForBuildExecutor,
            IBuildGraphArgumentGenerator buildGraphArgumentGenerator,
            IPreBuild preBuild)
        {
            _logger = logger;
            _buildGraphExecutor = buildGraphGenerator;
            _engineWorkspaceProvider = engineWorkspaceProvider;
            _workspaceProvider = workspaceProvider;
            _sdkSetupForBuildExecutor = sdkSetupForBuildExecutor;
            _buildGraphArgumentGenerator = buildGraphArgumentGenerator;
            _pluginPrepare = serviceProvider.GetServices<IDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider>>().ToArray();
            _projectPrepare = serviceProvider.GetServices<IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>>().ToArray();
            _preBuild = preBuild;
        }

        public string DiscoverPipelineId()
        {
            return string.Empty;
        }

        private sealed class DAGNode
        {
            public required BuildGraphExportNode Node { get; set; }

            public required BuildGraphExportGroup Group { get; set; }

            public Lazy<Task<BuildResultStatus>>? ThisTask { get; set; }

            public DAGDependency[]? DependsTasks { get; set; }
        }

        private sealed class DAGDependency
        {
            public required DAGNode Node { get; set; }
        }

        private async Task<IWorkspace> GetFolderWorkspaceAsync(
            string buildGraphRepositoryRoot,
            string nodeName)
        {
            if (_workspaceProvider.ProvidesFastCopyOnWrite && !string.IsNullOrWhiteSpace(buildGraphRepositoryRoot))
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new FolderSnapshotWorkspaceDescriptor
                    {
                        SourcePath = buildGraphRepositoryRoot,
                        WorkspaceDisambiguators = new[] { nodeName },
                    },
                    CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new FolderAliasWorkspaceDescriptor
                    {
                        AliasedPath = buildGraphRepositoryRoot
                    },
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task<BuildResultStatus> ExecuteDAGNode(
            BuildSpecification buildSpecification,
            BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>[]? preparePlugin,
            BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>[]? prepareProject,
            Dictionary<string, string> globalEnvironmentVariables,
            IBuildExecutionEvents buildExecutionEvents,
            DAGNode node,
            SemaphoreSlim? blockingSemaphore,
            ConcurrentBag<Lazy<Task<BuildResultStatus>>> allTasks,
            CancellationToken cancellationToken)
        {
            if (node.DependsTasks != null)
            {
                var dependencyResults = await Task.WhenAll(node.DependsTasks.Select(x => x.Node.ThisTask!.Value)).ConfigureAwait(false);
                if (!dependencyResults.All(x => x == BuildResultStatus.Success))
                {
                    _logger.LogTrace($"Skipped: {node.Node.Name} = NotRun");
                    return BuildResultStatus.NotRun;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (blockingSemaphore != null)
            {
                await blockingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Before we start work on this task, see if there are any unrelated
                // tasks that have failed. If they have, the build can never succeed
                // so there's no point starting new build nodes.
                foreach (var task in allTasks)
                {
                    if (task.IsValueCreated &&
                        ((task.Value.IsCompleted && task.Value.Result != BuildResultStatus.Success) ||
                        task.Value.IsFaulted ||
                        task.Value.IsCanceled))
                    {
                        _logger.LogTrace($"Skipped: {node.Node.Name} = NotRun");
                        return BuildResultStatus.NotRun;
                    }
                }

                if (node.Node.Name == "End")
                {
                    // This is a special node that is used in our built-in BuildGraphs
                    // to combine all of the required steps together. We don't actually
                    // need to run anything for it though.
                    await buildExecutionEvents.OnNodeStarted(node.Node.Name).ConfigureAwait(false);
                    await buildExecutionEvents.OnNodeFinished(node.Node.Name, BuildResultStatus.Success).ConfigureAwait(false);
                    return BuildResultStatus.Success;
                }

                await buildExecutionEvents.OnNodeStarted(node.Node.Name).ConfigureAwait(false);
                try
                {
                    await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                        buildSpecification.Engine,
                        string.Empty,
                        cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var engineWorkspace).ConfigureAwait(false))
                    {
                        var globalEnvironmentVariablesWithSdk = await _sdkSetupForBuildExecutor.SetupForBuildAsync(
                            buildSpecification,
                            node.Node.Name,
                            engineWorkspace.Path,
                            globalEnvironmentVariables,
                            cancellationToken).ConfigureAwait(false);

                        async Task<int> ExecuteBuildInWorkspaceAsync(string targetWorkspacePath)
                        {
                            var preBuildGraphArguments = _buildGraphArgumentGenerator.GeneratePreBuildGraphArguments(
                                buildSpecification.BuildGraphSettings,
                                buildSpecification.BuildGraphSettingReplacements,
                                targetWorkspacePath,
                                buildSpecification.UETPath,
                                engineWorkspace.Path,
                                OperatingSystem.IsWindows()
                                    ? buildSpecification.BuildGraphEnvironment.Windows.SharedStorageAbsolutePath
                                    : buildSpecification.BuildGraphEnvironment.Mac!.SharedStorageAbsolutePath,
                                buildSpecification.ArtifactExportPath);

                            {
                                var exitCode = await _preBuild.RunGeneralPreBuild(
                                    targetWorkspacePath,
                                    node.Node.Name,
                                    preBuildGraphArguments,
                                    cancellationToken).ConfigureAwait(false);
                                if (exitCode != 0)
                                {
                                    _logger.LogError($"General pre-build failed with exit code {exitCode}.");
                                    return exitCode;
                                }
                            }

                            if (preparePlugin != null && preparePlugin.Length > 0)
                            {
                                _logger.LogTrace($"Running plugin preparation steps for pre-BuildGraph hook.");
                                foreach (var byType in preparePlugin.GroupBy(x => x.Type))
                                {
                                    var provider = _pluginPrepare
                                        .Where(x => x.Type == byType.Key)
                                        .OfType<IPluginPrepareProvider>()
                                        .First();
                                    var exitCode = await provider.RunBeforeBuildGraphAsync(
                                        byType,
                                        targetWorkspacePath,
                                        preBuildGraphArguments,
                                        cancellationToken).ConfigureAwait(false);
                                    if (exitCode != 0)
                                    {
                                        _logger.LogError($"Plugin preparation step for pre-BuildGraph hook failed with exit code {exitCode}.");
                                        return exitCode;
                                    }
                                }
                            }

                            if (prepareProject != null && prepareProject.Length > 0)
                            {
                                _logger.LogTrace($"Running project preparation steps for pre-BuildGraph hook.");
                                foreach (var byType in prepareProject.GroupBy(x => x.Type))
                                {
                                    var provider = _projectPrepare
                                        .Where(x => x.Type == byType.Key)
                                        .OfType<IProjectPrepareProvider>()
                                        .First();
                                    var exitCode = await provider.RunBeforeBuildGraphAsync(
                                        byType,
                                        targetWorkspacePath,
                                        preBuildGraphArguments,
                                        cancellationToken).ConfigureAwait(false);
                                    if (exitCode != 0)
                                    {
                                        _logger.LogError($"Project preparation step for pre-BuildGraph hook failed with exit code {exitCode}.");
                                        return exitCode;
                                    }
                                }
                            }

                            _logger.LogTrace($"Starting: {node.Node.Name}");
                            return await _buildGraphExecutor.ExecuteGraphNodeAsync(
                                engineWorkspace!.Path,
                                targetWorkspacePath,
                                buildSpecification.UETPath,
                                buildSpecification.ArtifactExportPath,
                                buildSpecification.BuildGraphScript,
                                buildSpecification.BuildGraphTarget,
                                node.Node.Name,
                                OperatingSystem.IsWindows() ? buildSpecification.BuildGraphEnvironment.Windows.SharedStorageAbsolutePath : buildSpecification.BuildGraphEnvironment.Mac!.SharedStorageAbsolutePath,
                                buildSpecification.BuildGraphSettings,
                                buildSpecification.BuildGraphSettingReplacements,
                                globalEnvironmentVariablesWithSdk!,
                                buildSpecification.MobileProvisions,
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
                                cancellationToken).ConfigureAwait(false);
                        }

                        int exitCode;
                        if (buildSpecification.Engine.IsEngineBuild)
                        {
                            exitCode = await ExecuteBuildInWorkspaceAsync(engineWorkspace.Path).ConfigureAwait(false);
                        }
                        else
                        {
                            await using ((await GetFolderWorkspaceAsync(
                                buildSpecification.BuildGraphRepositoryRoot,
                                node.Node.Name).ConfigureAwait(false)).AsAsyncDisposable(out var targetWorkspace).ConfigureAwait(false))
                            {
                                exitCode = await ExecuteBuildInWorkspaceAsync(targetWorkspace.Path).ConfigureAwait(false);
                            }
                        }
                        if (exitCode == 0)
                        {
                            _logger.LogTrace($"Finished: {node.Node.Name} = Success");
                            await buildExecutionEvents.OnNodeFinished(node.Node.Name, BuildResultStatus.Success).ConfigureAwait(false);
                            return BuildResultStatus.Success;
                        }
                        else
                        {
                            _logger.LogTrace($"Finished: {node.Node.Name} = Failed");
                            await buildExecutionEvents.OnNodeFinished(node.Node.Name, BuildResultStatus.Failed).ConfigureAwait(false);
                            return BuildResultStatus.Failed;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The build was cancelled.
                    _logger.LogTrace($"Finished: {node.Node.Name} = Cancelled");
                    await buildExecutionEvents.OnNodeFinished(node.Node.Name, BuildResultStatus.Cancelled).ConfigureAwait(false);
                    return BuildResultStatus.Cancelled;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Internal exception while running build job {node.Node.Name}: {ex.Message}");
                    await buildExecutionEvents.OnNodeFinished(node.Node.Name, BuildResultStatus.Failed).ConfigureAwait(false);
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
            BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>[]? preparePlugin,
            BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>[]? prepareProject,
            IBuildExecutionEvents buildExecutionEvents,
            ICaptureSpecification generationCaptureSpecification,
            CancellationToken cancellationToken)
        {
            BuildGraphExport buildGraph;
            await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                buildSpecification.Engine,
                string.Empty,
                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var engineWorkspace).ConfigureAwait(false))
            {
                await using ((await GetFolderWorkspaceAsync(
                    buildSpecification.BuildGraphRepositoryRoot,
                    "Generate BuildGraph JSON").ConfigureAwait(false)).AsAsyncDisposable(out var targetWorkspace).ConfigureAwait(false))
                {
                    _logger.LogInformation("Generating BuildGraph JSON based on settings...");
                    buildGraph = await _buildGraphExecutor.GenerateGraphAsync(
                        engineWorkspace.Path,
                        targetWorkspace.Path,
                        buildSpecification.UETPath,
                        buildSpecification.ArtifactExportPath,
                        buildSpecification.BuildGraphScript,
                        buildSpecification.BuildGraphTarget,
                        OperatingSystem.IsWindows()
                            ? buildSpecification.BuildGraphEnvironment.Windows.SharedStorageAbsolutePath
                            : buildSpecification.BuildGraphEnvironment.Mac!.SharedStorageAbsolutePath,
                        buildSpecification.BuildGraphSettings,
                        buildSpecification.BuildGraphSettingReplacements,
                        generationCaptureSpecification,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            var globalEnvironmentVariables = buildSpecification.GlobalEnvironmentVariables ?? new Dictionary<string, string>();

            _logger.LogInformation("Executing build...");

            SemaphoreSlim? blockingSemaphore = new SemaphoreSlim(1);

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
            var allTasks = new ConcurrentBag<Lazy<Task<BuildResultStatus>>>();
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
                        return await ExecuteDAGNode(
                            buildSpecification,
                            preparePlugin,
                            prepareProject,
                            globalEnvironmentVariables,
                            buildExecutionEvents,
                            nodeCopy,
                            blockingSemaphore,
                            allTasks,
                            cancellationToken).ConfigureAwait(false);
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

            var allResults = await Task.WhenAll(allTasks.Select(x => x.Value)).ConfigureAwait(false);

            return allResults.All(x => x == BuildResultStatus.Success) ? 0 : 1;
        }
    }
}
