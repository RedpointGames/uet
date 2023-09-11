namespace Redpoint.Concurrency
{
    using System.Threading.Tasks;

    /// <summary>
    /// An implementation of a mutex that returns a disposable <see cref="IAcquiredLock"/> when the
    /// lock is acquired, providing a mutex that is usable with the <c>using</c> construct.
    /// </summary>
    public class Mutex
    {
        private readonly Semaphore _semaphore = new(1);

        /// <summary>
        /// Wait indefinitely to acquire the mutex in a blocking manner.
        /// </summary>
        /// <returns>The acquired lock, which must be disposed when no longer needed.</returns>
        public IAcquiredLock Wait(CancellationToken cancellationToken)
        {
            _semaphore.Wait(cancellationToken);
            return new MutexAcquiredLock(_semaphore);
        }

        /// <summary>
        /// Wait asynchronously to acquire the mutex.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the wait operation.</param>
        /// <returns>The acquired lock, which must be disposed when no longer needed.</returns>
        public async Task<IAcquiredLock> WaitAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new MutexAcquiredLock(_semaphore);
        }

        private sealed class MutexAcquiredLock : IAcquiredLock
        {
            private readonly Semaphore _semaphore;
            private bool _disposed;

            public MutexAcquiredLock(Semaphore semaphore)
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
