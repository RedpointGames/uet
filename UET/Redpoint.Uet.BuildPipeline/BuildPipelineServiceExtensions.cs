using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.Uet.BuildPipeline.Tests")]

namespace Redpoint.Uet.BuildPipeline
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Dynamic;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Gradle;
    using Redpoint.Uet.BuildPipeline.BuildGraph.MobileProvisioning;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Patching;
    using Redpoint.Uet.BuildPipeline.BuildGraph.PreBuild;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;

    public static class BuildPipelineServiceExtensions
    {
        public static void AddUETBuildPipeline(this IServiceCollection services)
        {
            services.AddSingleton<IBuildGraphPatcher, DefaultBuildGraphPatcher>();
            services.AddSingleton<IBuildGraphExecutor, DefaultBuildGraphExecutor>();
            services.AddSingleton<IBuildGraphArgumentGenerator, DefaultBuildGraphArgumentGenerator>();
            services.AddSingleton<IEngineWorkspaceProvider, DefaultEngineWorkspaceProvider>();
            services.AddSingleton<ISdkSetupForBuildExecutor, DefaultSdkSetupForBuildExecutor>();
            services.AddSingleton<IDynamicBuildGraphIncludeWriter, DefaultDynamicBuildGraphIncludeWriter>();
            services.AddSingleton<IPreBuild, DefaultPreBuild>();
            services.AddSingleton<IGradleWorkspace, DefaultGradleWorkspace>();
            services.AddSingleton<IDotnetLocator, DefaultDotnetLocator>();
            if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IMobileProvisioning, MacMobileProvisioning>();
            }
            else
            {
                services.AddSingleton<IMobileProvisioning, NullMobileProvisioning>();
            }
        }
    }
}