namespace Redpoint.Uefs.Daemon.Transactional
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class DefaultTransactionContext : ITransactionContext, IAsyncDisposable
    {
        private readonly DefaultTransactionalDatabase _database;
        protected readonly ITransaction _transaction;
        
        // @note: This is not a concurrent collection since the ITransactionContext is unique per transaction.
        private readonly List<IDisposable> _obtainedLocks;

        public ITransactionalDatabase Database => _database;

        public string? CurrentMountOperation
        {
            get { return _database._currentMountOperation; }
            set { _database._currentMountOperation = value; }
        }

        public DefaultTransactionContext(
            DefaultTransactionalDatabase database,
            ITransaction transaction)
        {
            _database = database;
            _transaction = transaction;
            _obtainedLocks = new List<IDisposable>();
        }

        private class LockWrapper : IDisposable
        {
            private readonly DefaultTransactionContext _context;
            private readonly IDisposable _underlyingLock;

            public LockWrapper(
                DefaultTransactionContext context,
                IDisposable underlyingLock)
            {
                _context = context;
                _underlyingLock = underlyingLock;
            }

            public void Dispose()
            {
                _underlyingLock.Dispose();
                _context._obtainedLocks.Remove(_underlyingLock);
            }
        }

        public async Task<IDisposable> ObtainLockAsync(string key, CancellationToken cancellationToken)
        {
            var @lock = await _database._semaphores.LockAsync(key, cancellationToken);
            try
            {
                _obtainedLocks.Add(new LockWrapper(this, @lock));
                return @lock;
            }
            catch
            {
                // I don't think the above code can throw, but just be careful to ensure we don't end up with locks that are never freed.
                @lock.Dispose();
                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            // @note: We make a copy of obtained locks since calling Dispose will mutate it.
            foreach (var @lock in _obtainedLocks.ToArray())
            {
                @lock.Dispose();
            }

            return ValueTask.CompletedTask;
        }

        public void UpdatePollingResponse(Func<PollingResponse, PollingResponse> pollingResponseUpdate)
        {
            _transaction.UpdatePollingResponse(
                new PollingResponse(
                    pollingResponseUpdate(_transaction.LatestPollingResponse)));
        }

        public void UpdatePollingResponse(Action<PollingResponse> pollingResponseUpdate)
        {
            var newPollingResponse = new PollingResponse(_transaction.LatestPollingResponse);
            pollingResponseUpdate(newPollingResponse);
            _transaction.UpdatePollingResponse(newPollingResponse);
        }
    }
}
