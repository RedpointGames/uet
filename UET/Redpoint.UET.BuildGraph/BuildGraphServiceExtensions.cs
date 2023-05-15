namespace Redpoint.UET.BuildGraph
{
    using Microsoft.Extensions.DependencyInjection;

    public static class BuildGraphServiceExtensions
    {
        public static void AddUETBuildGraph(this IServiceCollection services)
        {
            services.AddSingleton<IBuildGraphExecutor, DefaultBuildGraphExecutor>();
        }
    }
}