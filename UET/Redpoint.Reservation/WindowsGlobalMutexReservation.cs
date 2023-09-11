namespace Redpoint.Reservation
{
    using System.Runtime.Versioning;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    internal sealed class WindowsGlobalMutexReservation : IGlobalMutexReservation
    {
        private readonly Semaphore _semaphore;

        public WindowsGlobalMutexReservation(Semaphore semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
