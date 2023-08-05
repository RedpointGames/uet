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
            IWorkerPool workerPool,
            Graph graph,
            IAsyncStreamWriter<JobResponse> responseStream,
            CancellationToken cancellationToken);
    }
}
