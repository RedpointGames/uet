namespace Redpoint.Uet.BuildPipeline.BuildGraph.Dynamic
{
    using System.Threading.Tasks;

    public interface IDynamicBuildGraphIncludeWriter
    {
        Task WriteBuildGraphInclude(
            Stream stream,
            bool filterHostToCurrentPlatformOnly,
            object buildConfigDistribution,
            bool executeTests,
            bool executeDeployment);
    }
}
