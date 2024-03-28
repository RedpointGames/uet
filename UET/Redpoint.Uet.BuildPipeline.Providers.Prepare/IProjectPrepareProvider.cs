namespace Redpoint.Uet.BuildPipeline.Providers.Prepare
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;

    public interface IProjectPrepareProvider : IPrepareProvider, IDynamicProvider<BuildConfigProjectDistribution, IPrepareProvider>
    {
        Task<int> RunBeforeBuildGraphAsync(
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries,
            string repositoryRoot,
            IReadOnlyDictionary<string, string> preBuildGraphArguments,
            CancellationToken cancellationToken);
    }
}