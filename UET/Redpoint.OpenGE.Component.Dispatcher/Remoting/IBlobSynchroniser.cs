namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IBlobSynchroniser
    {
        Task<BlobHashingResult> HashInputBlobsAsync(
            RemoteTaskDescriptor remoteTaskDescriptor,
            CancellationToken cancellationToken);

        Task<BlobSynchronisationResult<InputFilesByBlobXxHash64>> SynchroniseInputBlobsAsync(
            ITaskApiWorkerCore workerCore,
            BlobHashingResult hashingResult,
            CancellationToken cancellationToken);

        Task<BlobSynchronisationResult> SynchroniseOutputBlobsAsync(
            ITaskApiWorkerCore workerCore,
            RemoteTaskDescriptor remoteTaskDescriptor,
            ExecuteTaskResponse finalExecuteTaskResponse,
            CancellationToken cancellationToken);
    }
}
