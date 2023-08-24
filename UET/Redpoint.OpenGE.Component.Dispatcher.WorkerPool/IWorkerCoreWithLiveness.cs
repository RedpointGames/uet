namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    public interface IWorkerCoreWithLiveness : IWorkerCore
    {
        ValueTask<bool> IsAliveAsync(CancellationToken cancellationToken);
    }
}
