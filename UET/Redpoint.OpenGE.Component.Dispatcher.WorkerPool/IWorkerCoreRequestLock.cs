namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System;
    using System.Threading.Tasks;

    public interface IWorkerCoreRequestLock<TWorkerCore> : IAsyncDisposable where TWorkerCore : IAsyncDisposable
    {
        IWorkerCoreRequest<TWorkerCore> Request { get; }

        Task FulfillRequestAsync(TWorkerCore core);
    }
}
