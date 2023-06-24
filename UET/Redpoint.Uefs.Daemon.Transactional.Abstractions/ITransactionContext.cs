namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using Redpoint.Uefs.Protocol;
    using System;
    using System.Threading.Tasks;

    public interface ITransactionContext
    {
        ITransactionalDatabase Database { get; }

        string? CurrentMountOperation { get; set; }

        Task<IDisposable> ObtainLockAsync(string key, CancellationToken cancellationToken);

        void UpdatePollingResponse(
            Func<PollingResponse, PollingResponse> pollingResponseUpdate);

        void UpdatePollingResponse(
            Action<PollingResponse> pollingResponseUpdate);
    }

    public interface ITransactionContext<TResult> : ITransactionContext
    {
        void UpdatePollingResponse(
            Func<PollingResponse, PollingResponse> pollingResponseUpdate,
            TResult? result);

        void UpdatePollingResponse(
            Action<PollingResponse> pollingResponseUpdate,
            TResult? result);
    }
}
