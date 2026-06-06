namespace Redpoint.Uet.BuildPipeline.BuildGraph.Build
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System.Xml;

    internal class DefaultProjectBuildProvider : IProjectBuildProvider
    {
        public Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            bool filterHostToCurrentPlatformOnly)
        {
            return Task.CompletedTask;
        }
    }
}
