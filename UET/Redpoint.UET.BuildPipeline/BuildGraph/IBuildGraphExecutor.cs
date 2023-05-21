namespace Redpoint.UET.BuildPipeline.BuildGraph
{
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph.Export;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IBuildGraphExecutor
    {
        Task<int> ExecuteGraphNodeAsync(
            string enginePath,
            string buildGraphRepositoryRootPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphNodeName,
            string buildGraphSharedStorageDir,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);

        Task<BuildGraphExport> GenerateGraphAsync(
            string enginePath,
            string buildGraphRepositoryRootPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}
