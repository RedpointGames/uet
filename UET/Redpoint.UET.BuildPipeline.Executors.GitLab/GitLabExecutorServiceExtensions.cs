namespace Redpoint.UET.BuildPipeline.Executors.Local
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.BuildPipeline.Executors.GitLab;

    public static class GitLabExecutorServiceExtensions
    {
        public static void AddUETBuildPipelineExecutorsGitLab(this IServiceCollection services)
        {
            services.AddSingleton<GitLabBuildExecutorFactory, GitLabBuildExecutorFactory>();
        }
    }
}
