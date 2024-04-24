namespace Redpoint.Uefs.Daemon.Transactional
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System;
    using System.Threading.Tasks;

    internal sealed class DefaultTransactionHandleWithResult<TResult> : ITransactionHandle<TResult> where TResult : class
    {
        private readonly string _transactionId;
        private readonly IWaitableTransaction<TResult> _transaction;
        private readonly IAsyncDisposable _listener;
        private readonly ILogger _logger;

        public DefaultTransactionHandleWithResult(
            string transactionId,
            IWaitableTransaction<TResult> transaction,
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

        public async Task<TResult> WaitForCompletionAsync(CancellationToken cancellationToken)
        {
            await _transaction.WaitForCompletionAsync(_logger, cancellationToken).ConfigureAwait(false);
            return _transaction.Result!;
        }
    }
}
