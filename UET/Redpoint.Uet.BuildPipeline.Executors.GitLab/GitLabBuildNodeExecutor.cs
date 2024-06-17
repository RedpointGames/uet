namespace Redpoint.Uet.BuildPipeline.Executors.GitLab
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Threading;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;
    using Redpoint.Uet.BuildPipeline.MultiWorkspace;

    public class GitLabBuildNodeExecutor : IBuildNodeExecutor
    {
        private readonly ILogger<GitLabBuildNodeExecutor> _logger;
        private readonly IBuildGraphExecutor _buildGraphExecutor;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
        private readonly IDynamicWorkspaceProvider _workspaceProvider;
        private readonly ISdkSetupForBuildExecutor _sdkSetupForBuildExecutor;
        private readonly IBuildGraphArgumentGenerator _buildGraphArgumentGenerator;
        private readonly IMultiWorkspaceAllocator _multiWorkspaceAllocator;
        private readonly IDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider>[] _pluginPrepare;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>[] _projectPrepare;

        public GitLabBuildNodeExecutor(
            IServiceProvider serviceProvider,
            ILogger<GitLabBuildNodeExecutor> logger,
            IBuildGraphExecutor buildGraphExecutor,
            IEngineWorkspaceProvider engineWorkspaceProvider,
            IDynamicWorkspaceProvider workspaceProvider,
            ISdkSetupForBuildExecutor sdkSetupForBuildExecutor,
            IBuildGraphArgumentGenerator buildGraphArgumentGenerator,
            IMultiWorkspaceAllocator multiWorkspaceAllocator)
        {
            _logger = logger;
            _buildGraphExecutor = buildGraphExecutor;
            _engineWorkspaceProvider = engineWorkspaceProvider;
            _workspaceProvider = workspaceProvider;
            _sdkSetupForBuildExecutor = sdkSetupForBuildExecutor;
            _buildGraphArgumentGenerator = buildGraphArgumentGenerator;
            _multiWorkspaceAllocator = multiWorkspaceAllocator;
            _pluginPrepare = serviceProvider.GetServices<IDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider>>().ToArray();
            _projectPrepare = serviceProvider.GetServices<IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>>().ToArray();
        }

        public string DiscoverPipelineId()
        {
            return System.Environment.GetEnvironmentVariable("CI_PIPELINE_ID") ?? string.Empty;
        }

        private class NodeNameExecutionState
        {
            public string? NodeName;
        }

        public async Task<int> ExecuteBuildNodesAsync(
            BuildSpecification buildSpecification,
            BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>[]? preparePlugin,
            BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>[]? prepareProject,
            IBuildExecutionEvents buildExecutionEvents,
            IReadOnlyList<string> nodeNames,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(buildSpecification);
            ArgumentNullException.ThrowIfNull(buildExecutionEvents);
            ArgumentNullException.ThrowIfNull(nodeNames);

            var repository = System.Environment.GetEnvironmentVariable("CI_REPOSITORY_URL")!;
            var commit = System.Environment.GetEnvironmentVariable("CI_COMMIT_SHA")!;
            var executingNode = new NodeNameExecutionState();

            var baseRepository = System.Environment.GetEnvironmentVariable("BASE_CI_REPOSITORY_URL");
            var baseCommit = System.Environment.GetEnvironmentVariable("BASE_CI_COMMIT_SHA");

            _logger.LogTrace("Starting execution of nodes...");
            try
            {
                await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                    buildSpecification.Engine,
                    string.Empty,
                    cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var engineWorkspace).ConfigureAwait(false))
                {
                    async Task<int> ExecuteNodeInWorkspaceAsync(
                        string nodeName,
                        RepositoryRoot repositoryRoot)
                    {
                        var buildGraphArgumentContext = new BuildGraphArgumentContext
                        {
                            RepositoryRoot = repositoryRoot,
                            UetPath = buildSpecification.UETPath,
                            EnginePath = engineWorkspace.Path,
                            SharedStoragePath = OperatingSystem.IsWindows()
                                ? buildSpecification.BuildGraphEnvironment.Windows.SharedStorageAbsolutePath
                                : buildSpecification.BuildGraphEnvironment.Mac!.SharedStorageAbsolutePath,
                            ArtifactExportPath = buildSpecification.ArtifactExportPath,
                        };

                        var globalEnvironmentVariablesWithSdk = await _sdkSetupForBuildExecutor.SetupForBuildAsync(
                            buildSpecification,
                            nodeName,
                            engineWorkspace.Path,
                            buildSpecification.GlobalEnvironmentVariables ?? new Dictionary<string, string>(),
                            cancellationToken).ConfigureAwait(false);

                        var preBuildGraphArguments = _buildGraphArgumentGenerator.GeneratePreBuildGraphArguments(
                            buildSpecification.BuildGraphSettings,
                            buildSpecification.BuildGraphSettingReplacements,
                            buildGraphArgumentContext);

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
                                    repositoryRoot.OutputPath,
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
                                    repositoryRoot.OutputPath,
                                    preBuildGraphArguments,
                                    cancellationToken).ConfigureAwait(false);
                                if (exitCode != 0)
                                {
                                    _logger.LogError($"Project preparation step for pre-BuildGraph hook failed with exit code {exitCode}.");
                                    return exitCode;
                                }
                            }
                        }

                        _logger.LogTrace($"Starting: {nodeName}");
                        return await _buildGraphExecutor.ExecuteGraphNodeAsync(
                            buildGraphArgumentContext,
                            buildSpecification.BuildGraphScript,
                            buildSpecification.BuildGraphTarget,
                            nodeName,
                            buildSpecification.BuildGraphSettings,
                            buildSpecification.BuildGraphSettingReplacements,
                            globalEnvironmentVariablesWithSdk,
                            buildSpecification.MobileProvisions,
                            CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                            {
                                ReceiveStdout = (line) =>
                                {
                                    buildExecutionEvents.OnNodeOutputReceived(nodeName, new[] { line });
                                    return false;
                                },
                                ReceiveStderr = (line) =>
                                {
                                    buildExecutionEvents.OnNodeOutputReceived(nodeName, new[] { line });
                                    return false;
                                },
                            }),
                            cancellationToken).ConfigureAwait(false);
                    }

                    async Task<int> ExecuteNodesInWorkspaceAsync(RepositoryRoot repositoryRoot)
                    {
                        foreach (var nodeName in nodeNames)
                        {
                            await buildExecutionEvents.OnNodeStarted(nodeName).ConfigureAwait(false);
                            executingNode.NodeName = nodeName;
                            var exitCode = await ExecuteNodeInWorkspaceAsync(
                                nodeName,
                                repositoryRoot).ConfigureAwait(false);
                            if (exitCode == 0)
                            {
                                _logger.LogTrace($"Finished: {nodeName} = Success");
                                executingNode.NodeName = null;
                                await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Success).ConfigureAwait(false);
                                continue;
                            }
                            else
                            {
                                _logger.LogTrace($"Finished: {nodeName} = Failed");
                                executingNode.NodeName = null;
                                await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Failed).ConfigureAwait(false);
                                return 1;
                            }
                        }
                        return 0;
                    }

                    int overallExitCode;
                    if (buildSpecification.Engine.IsEngineBuild)
                    {
                        overallExitCode = await ExecuteNodesInWorkspaceAsync(
                            new RepositoryRoot
                            {
                                BaseCodePath = engineWorkspace.Path,
                                PlatformCodePath = string.Empty,
                            }).ConfigureAwait(false);
                    }
                    else
                    {
                        await using (await _multiWorkspaceAllocator.AllocateAsync())

                            var workspaces = new Dictionary<string, IWorkspace?>();
                        try
                        {
                            _logger.LogTrace($"Obtaining workspace for build.");
                            workspaces.Add("base", await _workspaceProvider.GetWorkspaceAsync(
                                new GitWorkspaceDescriptor
                                {
                                    RepositoryUrl = repository,
                                    RepositoryCommitOrRef = commit,
                                    AdditionalFolderLayers = Array.Empty<string>(),
                                    AdditionalFolderZips = Array.Empty<string>(),
                                    WorkspaceDisambiguators = nodeNames,
                                    ProjectFolderName = buildSpecification.ProjectFolderName,
                                    IsEngineBuild = false,
                                    WindowsSharedGitCachePath = null,
                                    MacSharedGitCachePath = null,
                                },
                                cancellationToken).ConfigureAwait(false));

                            if (!string.IsNullOrWhiteSpace(baseRepository) &&
                                !string.IsNullOrWhiteSpace(baseCommit))
                            {
                                _logger.LogTrace($"Obtaining platform-specific workspace for build.");
                                workspaces.Add("platform", await _workspaceProvider.GetWorkspaceAsync(
                                    new GitWorkspaceDescriptor
                                    {
                                        RepositoryUrl = baseRepository,
                                        RepositoryCommitOrRef = baseCommit,
                                        AdditionalFolderLayers = Array.Empty<string>(),
                                        AdditionalFolderZips = Array.Empty<string>(),
                                        WorkspaceDisambiguators = nodeNames,
                                        ProjectFolderName = buildSpecification.ProjectFolderName,
                                        IsEngineBuild = false,
                                        WindowsSharedGitCachePath = null,
                                        MacSharedGitCachePath = null,
                                    },
                                    cancellationToken).ConfigureAwait(false));
                            }
                            else
                            {
                                workspaces.Add("platform", null);
                            }

                            _logger.LogTrace($"Calling ExecuteNodesInWorkspaceAsync inside allocated workspace.");
                            overallExitCode = await ExecuteNodesInWorkspaceAsync(new RepositoryRoot
                            {
                                BaseCodePath = workspaces["base"]!.Path,
                                PlatformCodePath = workspaces["platform"]?.Path ?? string.Empty,
                            }).ConfigureAwait(false);
                            _logger.LogTrace($"Finished ExecuteNodesInWorkspaceAsync with exit code '{overallExitCode}'.");
                        }
                        finally
                        {
                            foreach (var kv in workspaces)
                            {
                                if (kv.Value != null)
                                {
                                    await kv.Value.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                            _logger.LogTrace($"Released workspace for build.");
                        }
                    }
                    _logger.LogTrace($"Returning overall exit code '{overallExitCode}' from ExecuteBuildNodesAsync.");
                    return overallExitCode;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The build was cancelled.
                _logger.LogTrace("Detected build cancellation.");
                var currentNodeName = executingNode.NodeName;
                if (currentNodeName != null)
                {
                    _logger.LogTrace($"Finished: {currentNodeName} = Cancelled");
                    await buildExecutionEvents.OnNodeFinished(currentNodeName, BuildResultStatus.Cancelled).ConfigureAwait(false);
                }
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"Detected build failure due to exception: {ex}");
                var currentNodeName = executingNode.NodeName;
                if (currentNodeName != null)
                {
                    _logger.LogError(ex, $"Internal exception while running build job {currentNodeName}: {ex.Message}");
                    await buildExecutionEvents.OnNodeFinished(currentNodeName, BuildResultStatus.Failed).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogError(ex, $"Internal exception prior to running named build job: {ex.Message}");
                }
                return 1;
            }
        }
    }
}