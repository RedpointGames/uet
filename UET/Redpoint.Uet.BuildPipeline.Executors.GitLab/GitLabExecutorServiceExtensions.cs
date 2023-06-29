namespace Redpoint.Uet.BuildPipeline.Executors.Local
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.Executors.GitLab;

    public static class GitLabExecutorServiceExtensions
    {
        public static void AddUETBuildPipelineExecutorsGitLab(this IServiceCollection services)
        {
            services.AddSingleton<GitLabBuildExecutorFactory, GitLabBuildExecutorFactory>();
        }
    }
}
