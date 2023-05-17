using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.UET.BuildPipeline.Tests")]

namespace Redpoint.UET.BuildPipeline
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.BuildPipeline.BuildGraph;

    public static class BuildPipelineServiceExtensions
    {
        public static void AddUETBuildPipeline(this IServiceCollection services)
        {
            services.AddSingleton<IBuildGraphGenerator, DefaultBuildGraphGenerator>();
        }
    }
}