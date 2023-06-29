namespace Redpoint.Uet.BuildPipeline.Executors.Local
{
    using Microsoft.Extensions.DependencyInjection;

    public static class LocalExecutorServiceExtensions
    {
        public static void AddUETBuildPipelineExecutorsLocal(this IServiceCollection services)
        {
            services.AddSingleton<LocalBuildExecutorFactory, LocalBuildExecutorFactory>();
            services.AddTransient<LocalBuildExecutor, LocalBuildExecutor>();
        }
    }
}
