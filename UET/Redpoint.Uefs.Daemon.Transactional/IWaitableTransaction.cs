namespace Redpoint.Uefs.Daemon.Transactional
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System.Threading.Tasks;

    internal interface IWaitableTransaction : ITransaction
    {
        Task WaitForCompletionAsync(ILogger logger, CancellationToken cancellationToken);

        // @note: We need this on the interface because the full ITransaction interface
        // and the DefaultTransaction class both require the request type to be known.
        IAsyncDisposable RegisterListener(TransactionListener listenerDelegate, ILogger logger);
    }

    internal interface IWaitableTransaction<TResult> : IWaitableTransaction
    {
        TResult? Result { get; }

        // @note: We need these on this interface because the full ITransaction interface
        // and the DefaultTransaction class both require the request type to be known.
        IAsyncDisposable RegisterListener(TransactionListener<TResult> listenerDelegate, ILogger logger);

        void UpdatePollingResponse(PollingResponse pollingResponse, TResult? result);
    }
}
