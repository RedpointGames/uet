namespace Redpoint.Concurrency
{
    using System.Threading.Tasks;

    /// <summary>
    /// An implementation of a mutex that returns a disposable <see cref="IAcquiredLock"/> when the
    /// lock is acquired, providing a mutex that is usable with the <c>using</c> construct.
    /// </summary>
    public class MutexSlim
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        /// <summary>
        /// Wait to acquire the mutex.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the wait operation.</param>
        /// <returns>The acquired lock, which must be disposed when no longer needed.</returns>
        public async Task<IAcquiredLock> WaitAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            return new MutexAcquiredLock(_semaphore);
        }

        private class MutexAcquiredLock : IAcquiredLock
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _disposed = false;

            public MutexAcquiredLock(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _semaphore.Release();
                    _disposed = true;
                }
            }
        }
    }
}
