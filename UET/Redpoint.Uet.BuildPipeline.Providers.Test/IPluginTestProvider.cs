namespace Redpoint.Uet.BuildPipeline.Providers.Test
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;

    public interface IPluginTestProvider :
        ITestProvider,
        IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>
    {
    }
}