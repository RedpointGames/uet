namespace Redpoint.Uefs.Daemon.Transactional
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DefaultTransaction<TRequest> :
        AbstractTransaction<TRequest, TransactionListenerDelegate>,
        ITransaction<TRequest>,
        IWaitableTransaction
        where TRequest : class, ITransactionRequest
    {
        public DefaultTransaction(
            TRequest request,
            TransactionListenerDelegate initialListener,
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

        protected override Task InvokeListenerAsync(
            TransactionListenerDelegate @delegate,
            PollingResponse response)
        {
            return @delegate(response);
        }
    }
}
