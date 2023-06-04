namespace Redpoint.UET.BuildPipeline.Providers.Deployment
{
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Plugin;

    public interface IPluginDeploymentProvider : IDeploymentProvider, IDynamicProvider<BuildConfigPluginDistribution, IDeploymentProvider>
    {
    }
}
