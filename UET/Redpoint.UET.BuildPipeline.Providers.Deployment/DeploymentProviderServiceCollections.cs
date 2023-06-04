namespace Redpoint.UET.BuildPipeline.Providers.Deployment
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2;
    using Redpoint.UET.BuildPipeline.Providers.Deployment.Project.Custom;
    using Redpoint.UET.BuildPipeline.Providers.Deployment.Project.Steam;
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

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
