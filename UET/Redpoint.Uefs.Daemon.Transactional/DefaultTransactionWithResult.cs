namespace Redpoint.Uefs.Daemon.Transactional
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DefaultTransactionWithResult<TRequest, TResult> :
        AbstractTransaction<TRequest, TransactionListenerDelegate<TResult>>,
        ITransaction<TRequest, TResult>,
        IWaitableTransaction<TResult>
        where TRequest : class, ITransactionRequest<TResult> where TResult : class
    {
        public DefaultTransactionWithResult(
            TRequest request,
            TransactionListenerDelegate<TResult> initialListener,
            CancellationTokenSource executorCancellationTokenSource,
            bool backgroundable,
            SemaphoreSlim executorCompleteSemaphore) : base(
                request,
                initialListener,
                executorCancellationTokenSource,
                backgroundable,
                executorCompleteSemaphore)
        {
        }

        public TResult? Result { get; set; }

        public IAsyncDisposable RegisterListener(TransactionListenerDelegate listenerDelegate)
        {
            TransactionListenerDelegate<TResult> listenerDelegateWrapped = async (PollingResponse pollingResponse, TResult? _) =>
            {
                await listenerDelegate(pollingResponse);
            };
            _listeners.TryAdd(listenerDelegateWrapped, true);
            return new ReleasableListener(this, listenerDelegateWrapped);
        }

        public void UpdatePollingResponse(
            PollingResponse pollingResponse,
            TResult? result)
        {
            if (result != null)
            {
                Result = result;
            }
            UpdatePollingResponse(pollingResponse);
        }

        protected override Task InvokeListenerAsync(
            TransactionListenerDelegate<TResult> @delegate,
            PollingResponse response)
        {
            return @delegate(response, Result);
        }
    }
}
