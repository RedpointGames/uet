namespace Redpoint.UET.BuildPipeline.Executors.BuildServer
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBuildNodeExecutor
    {
        string NodeExecutorName { get; }

        Task<int> ExecuteBuildNodeAsync(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            string nodeName,
            string nodeParameters,
            CancellationToken cancellationToken);
    }
}