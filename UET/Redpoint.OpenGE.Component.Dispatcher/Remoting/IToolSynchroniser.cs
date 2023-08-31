namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IToolSynchroniser
    {
        Task<IHashedToolInfo> HashToolAsync(
            RemoteTaskDescriptor remoteTaskDescriptor,
            CancellationToken cancellationToken);

        Task<ToolExecutionInfo> SynchroniseToolAndGetXxHash64Async(
            ITaskApiWorkerCore workerCore,
            IHashedToolInfo hashedToolInfo,
            CancellationToken cancellationToken);
    }
}
