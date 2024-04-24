namespace Redpoint.Uefs.Daemon.Transactional
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System;
    using System.Threading.Tasks;

    internal sealed class DefaultTransactionHandle : ITransactionHandle
    {
        private readonly string _transactionId;
        private readonly IWaitableTransaction _transaction;
        private readonly IAsyncDisposable _listener;
        private readonly ILogger _logger;

        public DefaultTransactionHandle(
            string transactionId,
            IWaitableTransaction transaction,
            IAsyncDisposable listener,
            ILogger logger)
        {
            _transactionId = transactionId;
            _transaction = transaction;
            _listener = listener;
            _logger = logger;
        }

        public string TransactionId => _transactionId;

        public ValueTask DisposeAsync()
        {
            return _listener.DisposeAsync();
        }

        public Task WaitForCompletionAsync(CancellationToken cancellationToken)
        {
            return _transaction.WaitForCompletionAsync(_logger, cancellationToken);
        }
    }
}
