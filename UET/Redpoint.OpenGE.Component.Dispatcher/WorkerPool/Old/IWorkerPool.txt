namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System.Threading.Tasks;

    public interface IWorkerPool : IAsyncDisposable
    {
        Task<IWorkerCore> ReserveCoreAsync(
            bool requireLocalCore,
            CancellationToken cancellationToken);
    }
}
