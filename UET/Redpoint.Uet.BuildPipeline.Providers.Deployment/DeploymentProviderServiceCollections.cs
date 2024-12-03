namespace Redpoint.Uet.BuildPipeline.Providers.Deployment
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Steam;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Meta;

    public static class DeploymentProviderServiceCollections
    {
        public static void AddUETBuildPipelineProvidersDeployment(this IServiceCollection services)
        {
            services.AddDynamicProvider<BuildConfigPluginDistribution, IDeploymentProvider, BackblazeB2PluginDeploymentProvider>();
            services.AddDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider, CustomProjectDeploymentProvider>();
            services.AddDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider, SteamProjectDeploymentProvider>();
            services.AddDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider, MetaProjectDeploymentProvider>();
        }
    }
}
