namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.OpenGE.Protocol;

    internal interface IWorkerPoolFactory
    {
        IWorkerPool CreateWorkerPool(
            TaskApi.TaskApiClient localWorker);
    }
}
