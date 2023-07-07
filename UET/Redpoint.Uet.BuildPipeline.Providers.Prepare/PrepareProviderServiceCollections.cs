namespace Redpoint.Uet.BuildPipeline.Providers.Prepare
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;

    public static class PrepareProviderServiceCollections
    {
        public static void AddUetBuildPipelineProvidersPrepare(this IServiceCollection services)
        {
            services.AddDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider, CustomPluginPrepareProvider>();
            services.AddDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider, CustomProjectPrepareProvider>();
        }
    }
}