namespace Redpoint.UET.BuildPipeline.Executors.GitLab
{
    using Microsoft.Extensions.DependencyInjection;
    using System;

    public class GitLabBuildExecutorFactory : IBuildExecutorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public GitLabBuildExecutorFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IBuildExecutor CreateExecutor()
        {
            return _serviceProvider.GetRequiredService<GitLabBuildExecutor>();
        }
    }
}
