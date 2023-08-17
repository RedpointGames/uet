namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.OpenGE.Protocol;

    public interface IWorkerPoolFactory
    {
        IWorkerPool CreateWorkerPool(
            WorkerAddRequest localWorkerAddRequest);
    }
}
