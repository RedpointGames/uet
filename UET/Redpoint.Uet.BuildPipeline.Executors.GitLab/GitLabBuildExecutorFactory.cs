namespace Redpoint.Uet.BuildPipeline.Executors.GitLab
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.BuildGraph.PreBuild;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Core.Permissions;
    using Redpoint.Uet.Workspace;
    using System;

    public class GitLabBuildExecutorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public GitLabBuildExecutorFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IBuildExecutor CreateExecutor(string buildServerOutputFilePath)
        {
            return new GitLabBuildExecutor(
                _serviceProvider,
                _serviceProvider.GetRequiredService<ILogger<GitLabBuildExecutor>>(),
                buildServerOutputFilePath);
        }

        public IBuildNodeExecutor CreateNodeExecutor()
        {
            return new GitLabBuildNodeExecutor(
                _serviceProvider,
                _serviceProvider.GetRequiredService<ILogger<GitLabBuildNodeExecutor>>(),
                _serviceProvider.GetRequiredService<IBuildGraphExecutor>(),
                _serviceProvider.GetRequiredService<IEngineWorkspaceProvider>(),
                _serviceProvider.GetRequiredService<IDynamicWorkspaceProvider>(),
                _serviceProvider.GetRequiredService<ISdkSetupForBuildExecutor>(),
                _serviceProvider.GetRequiredService<IBuildGraphArgumentGenerator>(),
                _serviceProvider.GetRequiredService<IPreBuild>());
        }
    }
}
