namespace Redpoint.UET.BuildPipeline.BuildGraph
{
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph.Export;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal interface IBuildGraphGenerator
    {
        Task<BuildGraphExport> GenerateGraphAsync(
            string enginePath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            IEnumerable<string> buildGraphArguments,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}
