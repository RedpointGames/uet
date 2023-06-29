namespace Redpoint.Uet.BuildPipeline.Providers.Deployment
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Steam;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;

    public static class DeploymentProviderServiceCollections
    {
        public static void AddUETBuildPipelineProvidersDeployment(this IServiceCollection services)
        {
            services.AddSingleton<IDynamicProvider<BuildConfigPluginDistribution, IDeploymentProvider>, BackblazeB2PluginDeploymentProvider>();
            services.AddSingleton<IDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider>, CustomProjectDeploymentProvider>();
            services.AddSingleton<IDynamicProvider<BuildConfigProjectDistribution, IDeploymentProvider>, SteamProjectDeploymentProvider>();
        }
    }
}
