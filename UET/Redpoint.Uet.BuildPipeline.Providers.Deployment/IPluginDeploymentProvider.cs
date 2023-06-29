namespace Redpoint.Uet.BuildPipeline.Providers.Deployment
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;

    public interface IPluginDeploymentProvider : IDeploymentProvider, IDynamicProvider<BuildConfigPluginDistribution, IDeploymentProvider>
    {
    }
}
