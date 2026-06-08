namespace Redpoint.Uet.BuildPipeline.BuildGraph.Build
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Xml;

    internal interface IPluginBuildProvider
    {
        Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            bool filterHostToCurrentPlatformOnly);
    }
}
