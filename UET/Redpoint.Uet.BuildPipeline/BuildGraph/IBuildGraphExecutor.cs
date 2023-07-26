namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Export;
    using Redpoint.Uet.Configuration.Engine;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IBuildGraphExecutor
    {
        Task ListGraphAsync(
            string enginePath,
            BuildGraphScriptSpecification buildGraphScript,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);

        Task<int> ExecuteGraphNodeAsync(
            string enginePath,
            string buildGraphRepositoryRootPath,
            string uetPath,
            string artifactExportPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphNodeName,
            string buildGraphSharedStorageDir,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            Dictionary<string, string> globalEnvironmentVariables,
            BuildConfigMobileProvision[] mobileProvisions,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);

        Task<BuildGraphExport> GenerateGraphAsync(
            string enginePath,
            string buildGraphRepositoryRootPath,
            string uetPath,
            string artifactExportPath,
            BuildGraphScriptSpecification buildGraphScript,
            string buildGraphTarget,
            string buildGraphSharedStorageDir,
            Dictionary<string, string> buildGraphArguments,
            Dictionary<string, string> buildGraphArgumentReplacements,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken);
    }
}
