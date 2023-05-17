namespace Redpoint.UET.BuildPipeline.BuildGraph
{
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph.Export;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal interface IBuildGraphExecutor
    {
        Task<int> ExecuteGraphNodeAsync(
            string enginePath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphNodeName,
            string buildGraphArtifactPath,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);

        Task<BuildGraphExport> GenerateGraphAsync(
            string enginePath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}
