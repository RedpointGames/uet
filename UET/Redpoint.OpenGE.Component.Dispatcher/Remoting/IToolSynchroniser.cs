namespace Redpoint.OpenGE.Component.Dispatcher.Remoting
{
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using System.Threading.Tasks;

    internal interface IToolSynchroniser
    {
        Task<long> SynchroniseToolAndGetXxHash64(
            IWorkerCore workerCore,
            string path,
            CancellationToken cancellationToken);
    }
}
