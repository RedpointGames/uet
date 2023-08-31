namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System;
    using System.Threading.Tasks;

    public interface IWorkerCoreRequest<TWorkerCore> : IAsyncDisposable where TWorkerCore : IAsyncDisposable
    {
        CoreAllocationPreference CorePreference { get; }

        Task<TWorkerCore> WaitForCoreAsync(CancellationToken cancellationToken);
    }
}
