namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    public interface ITaskApiWorkerPoolFactory
    {
        ITaskApiWorkerPool CreateWorkerPool(TaskApiWorkerPoolConfiguration poolConfiguration);
    }
}
