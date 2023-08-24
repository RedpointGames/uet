namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    internal interface IWorkerCoreWithLiveness : IWorkerCore
    {
        ValueTask<bool> IsAliveAsync(CancellationToken cancellationToken);
    }
}
