namespace Redpoint.UET.BuildPipeline.BuildGraph.Dynamic
{
    using Redpoint.UET.Configuration;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface IDynamicBuildGraphIncludeWriter
    {
        Task WriteBuildGraphInclude(
            Stream stream,
            bool filterHostToCurrentPlatformOnly,
            object buildConfigDistribution);
    }
}
