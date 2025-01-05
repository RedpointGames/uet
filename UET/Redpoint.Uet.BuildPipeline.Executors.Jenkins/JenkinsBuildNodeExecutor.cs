namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class JenkinsBuildNodeExecutor : IBuildNodeExecutor
    {
        private readonly ILogger<JenkinsBuildNodeExecutor> _logger;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
        private readonly IDynamicWorkspaceProvider _workspaceProvider;

        public JenkinsBuildNodeExecutor(
            IServiceProvider serviceProvider,
            ILogger<JenkinsBuildNodeExecutor> logger,
            IEngineWorkspaceProvider engineWorkspaceProvider,
            IDynamicWorkspaceProvider workspaceProvider)
        {
            _logger = logger;
            _engineWorkspaceProvider = engineWorkspaceProvider;
            _workspaceProvider = workspaceProvider;
        }

        public string DiscoverPipelineId()
        {
            // TODO: This might be wrong if the ID is expected to be the same between the main executor and node executors, investigate.
            return Environment.GetEnvironmentVariable("BUILD_TAG") ?? string.Empty;
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

            var repository = Environment.GetEnvironmentVariable("UET_GIT_URL")!;
            var commit = Environment.GetEnvironmentVariable("UET_GIT_REF")!;
            var executingNode = new NodeNameExecutionState();

            _logger.LogInformation($"Repository: {repository}, branch: {commit}");

            _logger.LogTrace("Starting execution of nodes...");
            try
            {
                await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                    buildSpecification.Engine,
                    string.Empty,
                    cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var engineWorkspace).ConfigureAwait(false))
                {
                    async Task<int> ExecuteNodeInWorkspaceAsync(string nodeName, string targetWorkspacePath)
                    {
                        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                        throw new NotImplementedException();
                    }

                    async Task<int> ExecuteNodesInWorkspaceAsync(string targetWorkspacePath)
                    {
                        foreach (var nodeName in nodeNames)
                        {
                            await buildExecutionEvents.OnNodeStarted(nodeName).ConfigureAwait(false);
                            executingNode.NodeName = nodeName;
                            var exitCode = await ExecuteNodeInWorkspaceAsync(nodeName, targetWorkspacePath).ConfigureAwait(false);
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
                        overallExitCode = await ExecuteNodesInWorkspaceAsync(engineWorkspace.Path).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogTrace($"Obtaining workspace for build.");
                        await using ((await _workspaceProvider.GetWorkspaceAsync(
                            new GitWorkspaceDescriptor
                            {
                                RepositoryUrl = repository,
                                RepositoryCommitOrRef = commit,
                                AdditionalFolderLayers = Array.Empty<string>(),
                                AdditionalFolderZips = Array.Empty<string>(),
                                WorkspaceDisambiguators = nodeNames,
                                ProjectFolderName = buildSpecification.ProjectFolderName,
                                BuildType = GitWorkspaceDescriptorBuildType.Generic,
                                WindowsSharedGitCachePath = null,
                                MacSharedGitCachePath = null,
                            },
                            cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var targetWorkspace).ConfigureAwait(false))
                        {
                            _logger.LogTrace($"Calling ExecuteNodesInWorkspaceAsync inside allocated workspace.");
                            overallExitCode = await ExecuteNodesInWorkspaceAsync(targetWorkspace.Path).ConfigureAwait(false);
                            _logger.LogTrace($"Finished ExecuteNodesInWorkspaceAsync with exit code '{overallExitCode}'.");
                        }
                        _logger.LogTrace($"Released workspace for build.");
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
