namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    using Grpc.Core;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IGraphExecutor
    {
        Task ExecuteGraphAsync(
            ITaskApiWorkerPool workerPool,
            Graph graph,
            JobBuildBehaviour buildBehaviour,
            IGuardedResponseStream<JobResponse> responseStream,
            CancellationToken cancellationToken);
    }
}
