namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IToolSynchroniser
    {
        Task<ToolExecutionInfo> SynchroniseToolAndGetXxHash64(
            IWorkerCore workerCore,
            string path,
            CancellationToken cancellationToken);
    }
}
