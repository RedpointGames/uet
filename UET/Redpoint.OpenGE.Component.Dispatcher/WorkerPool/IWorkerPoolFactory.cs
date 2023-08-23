namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.OpenGE.Protocol;

    public interface IWorkerPoolFactory
    {
        IWorkerPool CreateWorkerPool();

        IWorkerPool CreateWorkerPool(
            WorkerAddRequest localWorkerAddRequest);
    }
}
