namespace Redpoint.UET.BuildPipeline.Executors
{
    using Grpc.Core.Logging;
    using Redpoint.ProcessExecution;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    public interface IBuildExecutor
    {
        Task<int> ExecuteBuildAsync(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            ICaptureSpecification generationCaptureSpecification,
            CancellationToken cancellationToken);
    }
}
