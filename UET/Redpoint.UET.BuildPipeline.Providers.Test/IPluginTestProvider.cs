namespace Redpoint.UET.BuildPipeline.Providers.Test
{
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Plugin;

    public interface IPluginTestProvider :
        ITestProvider,
        IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>
    {
    }
}