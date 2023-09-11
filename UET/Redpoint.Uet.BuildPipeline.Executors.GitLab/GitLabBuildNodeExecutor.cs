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

    public class GitLabBuildNodeExecutor : IBuildNodeExecutor
    {
        private readonly ILogger<GitLabBuildNodeExecutor> _logger;
        private readonly IBuildGraphExecutor _buildGraphExecutor;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
        private readonly IDynamicWorkspaceProvider _workspaceProvider;
        private readonly ISdkSetupForBuildExecutor _sdkSetupForBuildExecutor;
        private readonly IDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider>[] _pluginPrepare;
        private readonly IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>[] _projectPrepare;

        public GitLabBuildNodeExecutor(
            IServiceProvider serviceProvider,
            ILogger<GitLabBuildNodeExecutor> logger,
            IBuildGraphExecutor buildGraphExecutor,
            IEngineWorkspaceProvider engineWorkspaceProvider,
            IDynamicWorkspaceProvider workspaceProvider,
            ISdkSetupForBuildExecutor sdkSetupForBuildExecutor)
        {
            _logger = logger;
            _buildGraphExecutor = buildGraphExecutor;
            _engineWorkspaceProvider = engineWorkspaceProvider;
            _workspaceProvider = workspaceProvider;
            _sdkSetupForBuildExecutor = sdkSetupForBuildExecutor;
            _pluginPrepare = serviceProvider.GetServices<IDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider>>().ToArray();
            _projectPrepare = serviceProvider.GetServices<IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>>().ToArray();
        }

        public string DiscoverPipelineId()
        {
            return System.Environment.GetEnvironmentVariable("CI_PIPELINE_ID") ?? string.Empty;
        }

        public async Task<int> ExecuteBuildNodeAsync(
            BuildSpecification buildSpecification,
            BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>[]? preparePlugin,
            BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>[]? prepareProject,
            IBuildExecutionEvents buildExecutionEvents,
            string nodeName,
            CancellationToken cancellationToken)
        {
            if (buildSpecification == null) throw new ArgumentNullException(nameof(buildSpecification));
            if (buildExecutionEvents == null) throw new ArgumentNullException(nameof(buildExecutionEvents));

            var repository = System.Environment.GetEnvironmentVariable("CI_REPOSITORY_URL")!;
            var commit = System.Environment.GetEnvironmentVariable("CI_COMMIT_SHA")!;

            await buildExecutionEvents.OnNodeStarted(nodeName).ConfigureAwait(false);
            try
            {
                await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                    buildSpecification.Engine,
                    string.Empty,
                    cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var engineWorkspace).ConfigureAwait(false))
                {
                    var globalEnvironmentVariablesWithSdk = await _sdkSetupForBuildExecutor.SetupForBuildAsync(
                        buildSpecification,
                        nodeName,
                        engineWorkspace.Path,
                        buildSpecification.GlobalEnvironmentVariables ?? new Dictionary<string, string>(),
                        cancellationToken).ConfigureAwait(false);

                    async Task<int> ExecuteBuildInWorkspaceAsync(string targetWorkspacePath)
                    {
                        if (preparePlugin != null && preparePlugin.Length > 0)
                        {
                            _logger.LogTrace($"Running plugin preparation steps for pre-BuildGraph hook.");
                            foreach (var byType in preparePlugin.GroupBy(x => x.Type))
                            {
                                var provider = _pluginPrepare
                                    .Where(x => x.Type == byType.Key)
                                    .OfType<IPluginPrepareProvider>()
                                    .First();
                                await provider.RunBeforeBuildGraphAsync(
                                    byType,
                                    targetWorkspacePath,
                                    cancellationToken).ConfigureAwait(false);
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
                                await provider.RunBeforeBuildGraphAsync(
                                    byType,
                                    targetWorkspacePath,
                                    cancellationToken).ConfigureAwait(false);
                            }
                        }

                        _logger.LogTrace($"Starting: {nodeName}");
                        return await _buildGraphExecutor.ExecuteGraphNodeAsync(
                            engineWorkspace.Path,
                            targetWorkspacePath,
                            buildSpecification.UETPath,
                            buildSpecification.ArtifactExportPath,
                            buildSpecification.BuildGraphScript,
                            buildSpecification.BuildGraphTarget,
                            nodeName,
                            OperatingSystem.IsWindows() ? buildSpecification.BuildGraphEnvironment.Windows.SharedStorageAbsolutePath : buildSpecification.BuildGraphEnvironment.Mac!.SharedStorageAbsolutePath,
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

                    int exitCode;
                    if (buildSpecification.Engine.IsEngineBuild)
                    {
                        exitCode = await ExecuteBuildInWorkspaceAsync(engineWorkspace.Path).ConfigureAwait(false);
                    }
                    else
                    {
                        await using ((await _workspaceProvider.GetWorkspaceAsync(
                            new GitWorkspaceDescriptor
                            {
                                RepositoryUrl = repository,
                                RepositoryCommitOrRef = commit,
                                AdditionalFolderLayers = Array.Empty<string>(),
                                AdditionalFolderZips = Array.Empty<string>(),
                                WorkspaceDisambiguators = new[] { nodeName },
                                ProjectFolderName = buildSpecification.ProjectFolderName,
                                IsEngineBuild = false,
                                WindowsSharedGitCachePath = null,
                                MacSharedGitCachePath = null,
                            },
                            cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var targetWorkspace).ConfigureAwait(false))
                        {
                            exitCode = await ExecuteBuildInWorkspaceAsync(targetWorkspace.Path).ConfigureAwait(false);
                        }
                    }
                    if (exitCode == 0)
                    {
                        _logger.LogTrace($"Finished: {nodeName} = Success");
                        await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Success).ConfigureAwait(false);
                        return 0;
                    }
                    else
                    {
                        _logger.LogTrace($"Finished: {nodeName} = Failed");
                        await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Failed).ConfigureAwait(false);
                        return 1;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The build was cancelled.
                _logger.LogTrace($"Finished: {nodeName} = Cancelled");
                await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Cancelled).ConfigureAwait(false);
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Internal exception while running build job {nodeName}: {ex.Message}");
                await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Failed).ConfigureAwait(false);
                return 1;
            }
        }
    }
}