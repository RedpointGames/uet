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
        private readonly List<IDisposable> _obtainedLocks;
        private bool _disposed;

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
            _disposed = false;
        }

        private class LockWrapper : IDisposable
        {
            private readonly DefaultTransactionContext _context;
            private readonly string _key;
            private readonly IDisposable _underlyingLock;
            private bool _disposed;

            public LockWrapper(
                DefaultTransactionContext context,
                string key,
                IDisposable underlyingLock)
            {
                _context = context;
                _key = key;
                _underlyingLock = underlyingLock;
                _disposed = false;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(typeof(LockWrapper).Name);
                }
                _disposed = true;
                _underlyingLock.Dispose();
                _context._obtainedLocks.Remove(this);
            }
        }

        public async Task<IDisposable> ObtainLockAsync(string key, CancellationToken cancellationToken)
        {
            var @lock = await _database.GetInternalLockAsync(key, cancellationToken);
            var lockWrapper = new LockWrapper(this, key, @lock);
            _obtainedLocks.Add(lockWrapper);
            return lockWrapper;
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(typeof(DefaultTransactionContext).Name);
            }
            _disposed = true;

            // @note: We make a copy of obtained locks since calling Dispose will mutate it.
            foreach (var @lock in _obtainedLocks.ToArray())
            {
                @lock.Dispose();
            }
            _obtainedLocks.Clear();

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
