namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using Microsoft.Extensions.DependencyInjection;

    public static class JenkinsExecutorServiceExtensions
    {
        public static void AddUETBuildPipelineExecutorsJenkins(this IServiceCollection services)
        {
            services.AddSingleton<JenkinsBuildExecutorFactory, JenkinsBuildExecutorFactory>();
        }
    }
}
