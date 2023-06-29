namespace Redpoint.Uet.BuildPipeline.Executors
{
    using Redpoint.ProcessExecution;
    using System.Threading.Tasks;

    public interface IBuildExecutor
    {
        string DiscoverPipelineId();

        Task<int> ExecuteBuildAsync(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            ICaptureSpecification generationCaptureSpecification,
            CancellationToken cancellationToken);
    }
}
