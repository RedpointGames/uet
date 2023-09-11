namespace Redpoint.Uefs.Daemon.Transactional
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System;

    internal sealed class DefaultTransactionContextWithResult<TResult> : DefaultTransactionContext, ITransactionContext<TResult>, IAsyncDisposable
    {
        public DefaultTransactionContextWithResult(DefaultTransactionalDatabase database, IWaitableTransaction<TResult> transaction) : base(database, transaction)
        {
        }

        public void UpdatePollingResponse(Func<PollingResponse, PollingResponse> pollingResponseUpdate, TResult? result)
        {
            var transaction = (IWaitableTransaction<TResult>)_transaction;
            transaction.UpdatePollingResponse(
                new PollingResponse(
                    pollingResponseUpdate(transaction.LatestPollingResponse)),
                result);
        }

        public void UpdatePollingResponse(Action<PollingResponse> pollingResponseUpdate, TResult? result)
        {
            var transaction = (IWaitableTransaction<TResult>)_transaction;
            var newPollingResponse = new PollingResponse(transaction.LatestPollingResponse);
            pollingResponseUpdate(newPollingResponse);
            transaction.UpdatePollingResponse(
                newPollingResponse,
                result);
        }
    }
}
