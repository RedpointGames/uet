using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.UET.BuildPipeline.Tests")]

namespace Redpoint.UET.BuildPipeline
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.BuildGraph.Patching;
    using Redpoint.UET.BuildPipeline.Executors.Engine;

    public static class BuildPipelineServiceExtensions
    {
        public static void AddUETBuildPipeline(this IServiceCollection services)
        {
            services.AddSingleton<IBuildGraphPatcher, DefaultBuildGraphPatcher>();
            services.AddSingleton<IBuildGraphExecutor, DefaultBuildGraphExecutor>();
            services.AddSingleton<IBuildGraphArgumentGenerator, DefaultBuildGraphArgumentGenerator>();
            services.AddSingleton<IEngineWorkspaceProvider, DefaultEngineWorkspaceProvider>();
        }
    }
}