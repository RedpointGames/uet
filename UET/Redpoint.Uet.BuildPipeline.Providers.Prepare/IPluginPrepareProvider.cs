namespace Redpoint.Uet.BuildPipeline.Providers.Prepare
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;

    public interface IPluginPrepareProvider : IPrepareProvider,
        IDynamicProvider<BuildConfigPluginDistribution, IPrepareProvider>
    {
        Task RunBeforeBuildGraphAsync(
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>> entries,
            string repositoryRoot, CancellationToken cancellationToken);
    }
}