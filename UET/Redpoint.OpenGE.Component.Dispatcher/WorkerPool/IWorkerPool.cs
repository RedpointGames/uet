namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System.Threading.Tasks;

    internal interface IWorkerPool : IAsyncDisposable
    {
        Task<IWorkerCore> ReserveRemoteOrLocalCoreAsync(CancellationToken cancellationToken);

        Task<IWorkerCore> ReserveLocalCoreAsync(CancellationToken cancellationToken);
    }
}
