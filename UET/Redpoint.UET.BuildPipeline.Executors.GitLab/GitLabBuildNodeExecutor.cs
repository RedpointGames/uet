namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.Executors.BuildServer;
    using Redpoint.UET.BuildPipeline.Executors.Engine;
    using Redpoint.UET.Workspace;
    using Redpoint.UET.Workspace.Descriptors;
    using System.Threading;
    using System.Threading.Tasks;

    public class GitLabBuildNodeExecutor : IBuildNodeExecutor
    {
        private readonly ILogger<GitLabBuildNodeExecutor> _logger;
        private readonly IBuildGraphExecutor _buildGraphExecutor;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
        private readonly IDynamicWorkspaceProvider _workspaceProvider;
        private readonly ISdkSetupForBuildExecutor _sdkSetupForBuildExecutor;

        public GitLabBuildNodeExecutor(
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
        }

        public async Task<int> ExecuteBuildNodeAsync(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            string nodeName,
            string? projectFolderName,
            CancellationToken cancellationToken)
        {
            var repository = System.Environment.GetEnvironmentVariable("CI_REPOSITORY_URL")!;
            var commit = System.Environment.GetEnvironmentVariable("CI_COMMIT_SHA")!;

            await buildExecutionEvents.OnNodeStarted(nodeName);
            try
            {
                await using (var engineWorkspace = await _engineWorkspaceProvider.GetEngineWorkspace(
                    buildSpecification.Engine,
                    string.Empty,
                    cancellationToken))
                {
                    var globalEnvironmentVariablesWithSdk = await _sdkSetupForBuildExecutor.SetupForBuildAsync(
                        buildSpecification,
                        nodeName,
                        engineWorkspace.Path,
                        buildSpecification.GlobalEnvironmentVariables ?? new Dictionary<string, string>(),
                        cancellationToken);

                    int exitCode;
                    await using (var targetWorkspace = await _workspaceProvider.GetWorkspaceAsync(
                        new GitWorkspaceDescriptor
                        {
                            RepositoryUrl = repository,
                            RepositoryCommit = commit,
                            AdditionalFolderLayers = new string[0],
                            WorkspaceDisambiguators = new[] { nodeName },
                            ProjectFolderName = projectFolderName,
                        },
                        cancellationToken))
                    {
                        _logger.LogTrace($"Starting: {nodeName}");
                        exitCode = await _buildGraphExecutor.ExecuteGraphNodeAsync(
                            engineWorkspace.Path,
                            targetWorkspace.Path,
                            buildSpecification.UETPath,
                            buildSpecification.BuildGraphScript,
                            buildSpecification.BuildGraphTarget,
                            nodeName,
                            OperatingSystem.IsWindows() ? buildSpecification.BuildGraphEnvironment.Windows.SharedStorageAbsolutePath : buildSpecification.BuildGraphEnvironment.Mac!.SharedStorageAbsolutePath,
                            buildSpecification.BuildGraphSettings,
                            buildSpecification.BuildGraphSettingReplacements,
                            globalEnvironmentVariablesWithSdk,
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
                            cancellationToken);
                    }
                    if (exitCode == 0)
                    {
                        _logger.LogTrace($"Finished: {nodeName} = Success");
                        await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Success);
                        return 0;
                    }
                    else
                    {
                        _logger.LogTrace($"Finished: {nodeName} = Failed");
                        await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Failed);
                        return 1;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The build was cancelled.
                _logger.LogTrace($"Finished: {nodeName} = Cancelled");
                await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Cancelled);
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Internal exception while running build job {nodeName}: {ex.Message}");
                await buildExecutionEvents.OnNodeFinished(nodeName, BuildResultStatus.Failed);
                return 1;
            }
        }
    }
}