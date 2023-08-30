namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System;

    public interface IWorkerCoreRequestCollectionLock<TWorkerCore> : IAsyncDisposable where TWorkerCore : IAsyncDisposable
    {
        IEnumerable<IWorkerCoreRequestLock<TWorkerCore>> Requests { get; }
    }
}
