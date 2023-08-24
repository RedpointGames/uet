namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System.Threading.Tasks;

    public interface IWorkerCoreProvider<TWorkerCore>
    {
        string Id { get; }

        Task<TWorkerCore> RequestCoreAsync(CancellationToken cancellationToken);
    }
}
