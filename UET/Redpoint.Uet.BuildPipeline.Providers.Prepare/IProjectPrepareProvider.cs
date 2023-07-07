namespace Redpoint.Uet.BuildPipeline.Providers.Prepare
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;

    public interface IProjectPrepareProvider : IPrepareProvider, IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>
    {
        Task RunBeforeBuildGraphAsync(
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries,
            CancellationToken cancellationToken);
    }
}