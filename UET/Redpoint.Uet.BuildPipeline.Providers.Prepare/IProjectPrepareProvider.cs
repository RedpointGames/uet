namespace Redpoint.Uet.BuildPipeline.Providers.Prepare
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;

    public interface IProjectPrepareProvider : IPrepareProvider, IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>
    {
        Task RunBeforeBuildGraphAsync(
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries,
            string repositoryRoot,
            CancellationToken cancellationToken);
    }
}