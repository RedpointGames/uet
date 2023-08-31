namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;

    public interface IDispatcherComponentFactory
    {
        IDispatcherComponent Create(
            ITaskApiWorkerPool workerPool,
            string? pipeName);
    }
}
