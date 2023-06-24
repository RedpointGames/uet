namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using System;
    using System.Threading.Tasks;

    public interface ITransactionHandle : IAsyncDisposable
    {
        // @note: DisposeAsync on this implementation needs
        // to remove any listeners that were associated in
        // BeginTransactionAsync or via the ITransactionHandle
        // (and we need to add an API to do that maybe?)

        string TransactionId { get; }

        Task WaitForCompletionAsync(CancellationToken cancellationToken);
    }

    public interface ITransactionHandle<TResult> : IAsyncDisposable
    {
        string TransactionId { get; }

        Task<TResult> WaitForCompletionAsync(CancellationToken cancellationToken);
    }
}
