namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System;
    using System.Threading.Tasks;

    public interface IWorkerCoreRequest<TWorkerCore> : IAsyncDisposable where TWorkerCore : IAsyncDisposable
    {
        DateTime DateRequestedUtc { get; }

        CoreAllocationPreference CorePreference { get; }

        Task<TWorkerCore> WaitForCoreAsync(CancellationToken cancellationToken);
    }
}
