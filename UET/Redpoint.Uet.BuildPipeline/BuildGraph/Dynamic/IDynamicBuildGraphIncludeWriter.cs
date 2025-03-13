namespace Redpoint.Uet.BuildPipeline.BuildGraph.Dynamic
{
    using System.Threading.Tasks;

    public interface IDynamicBuildGraphIncludeWriter
    {
        Task WriteBuildGraphNodeInclude(
            Stream stream,
            bool filterHostToCurrentPlatformOnly,
            object buildConfig,
            object buildConfigDistribution,
            string[]? executeTests,
            string[]? executeDeployments);

        Task WriteBuildGraphMacroInclude(
            Stream stream,
            bool filterHostToCurrentPlatformOnly,
            object buildConfig,
            object buildConfigDistribution);
    }
}
