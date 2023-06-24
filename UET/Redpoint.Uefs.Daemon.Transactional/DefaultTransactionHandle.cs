namespace Redpoint.Uefs.Daemon.Transactional
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System;
    using System.Threading.Tasks;

    internal class DefaultTransactionHandle : ITransactionHandle
    {
        private readonly string _transactionId;
        private readonly IWaitableTransaction _transaction;
        private readonly IAsyncDisposable _listener;

        public DefaultTransactionHandle(
            string transactionId,
            IWaitableTransaction transaction,
            IAsyncDisposable listener)
        {
            _transactionId = transactionId;
            _transaction = transaction;
            _listener = listener;
        }

        public string TransactionId => _transactionId;

        public ValueTask DisposeAsync()
        {
            return _listener.DisposeAsync();
        }

        public Task WaitForCompletionAsync(CancellationToken cancellationToken)
        {
            return _transaction.WaitForCompletionAsync(cancellationToken);
        }
    }
}
