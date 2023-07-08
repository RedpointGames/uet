namespace Redpoint.Uet.BuildPipeline.BuildGraph.Dynamic
{
    using System.Threading.Tasks;

    public interface IDynamicBuildGraphIncludeWriter
    {
        Task WriteBuildGraphNodeInclude(
            Stream stream,
            bool filterHostToCurrentPlatformOnly,
            object buildConfigDistribution,
            bool executeTests,
            bool executeDeployment);

        Task WriteBuildGraphMacroInclude(
            Stream stream,
            bool filterHostToCurrentPlatformOnly,
            object buildConfigDistribution);
    }
}
