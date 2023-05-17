namespace Redpoint.UET.BuildPipeline.Executors
{
    using Redpoint.ProcessExecution;
    using System.Threading.Tasks;

    public interface IBuildExecutor
    {
        Task<int> ExecuteBuildAsync(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            ICaptureSpecification generationCaptureSpecification,
            CancellationToken cancellationToken);
    }
}
