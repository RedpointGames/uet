namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IBlobSynchroniser
    {
        Task<BlobSynchronisationResult<InputFilesByBlobXxHash64>> SynchroniseInputBlobs(
            IWorkerCore workerCore,
            RemoteTaskDescriptor remoteTaskDescriptor,
            CancellationToken cancellationToken);

        Task<BlobSynchronisationResult> SynchroniseOutputBlobs(
            IWorkerCore workerCore,
            RemoteTaskDescriptor remoteTaskDescriptor,
            ExecuteTaskResponse finalExecuteTaskResponse,
            CancellationToken cancellationToken);
    }
}
