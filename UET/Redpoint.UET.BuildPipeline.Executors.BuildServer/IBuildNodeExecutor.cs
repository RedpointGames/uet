namespace Redpoint.UET.BuildPipeline.Executors.BuildServer
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBuildNodeExecutor
    {
        Task<int> ExecuteBuildNodeAsync(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            string nodeName,
            string? projectFolderName,
            CancellationToken cancellationToken);
    }
}