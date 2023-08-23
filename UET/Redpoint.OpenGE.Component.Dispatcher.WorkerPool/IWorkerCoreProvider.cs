namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System.Threading.Tasks;

    internal interface IWorkerCoreProvider<TWorkerCore>
    {
        Task<TWorkerCore> RequestCoreAsync(CancellationToken cancellationToken);
    }
}
