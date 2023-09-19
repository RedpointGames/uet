namespace Redpoint.Reservation
{
    using System.Runtime.Versioning;
    using System.Threading.Tasks;
    using Mutex = Redpoint.Concurrency.Mutex;

    [SupportedOSPlatform("windows")]
    internal sealed class WindowsGlobalMutexReservation : IGlobalMutexReservation
    {
        private readonly Semaphore _semaphore;
        private readonly Mutex _semaphoreReleaseMutex;
        private bool _disposed;

        public WindowsGlobalMutexReservation(Semaphore semaphore)
        {
            _semaphore = semaphore;
            _semaphoreReleaseMutex = new Mutex();
        }

        public async ValueTask DisposeAsync()
        {
            using (await _semaphoreReleaseMutex.WaitAsync(CancellationToken.None).ConfigureAwait(false))
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
