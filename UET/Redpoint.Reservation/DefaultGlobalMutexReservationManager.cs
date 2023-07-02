namespace Redpoint.Reservation
{
    using System.Runtime.Versioning;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    internal class DefaultGlobalMutexReservationManager : IGlobalMutexReservationManager
    {
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("linux")]
        private readonly IReservationManager? _reservationManager;

        public DefaultGlobalMutexReservationManager(IReservationManagerFactory reservationManagerFactory)
        {
            if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                _reservationManager = reservationManagerFactory.CreateReservationManager(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    ".redpoint-reservation-mutex"));
            }
        }

        /// <remarks>
        /// The names of global mutexes can be anything; they don't need to conform to the
        /// requirements of file names or be case insensitive. Thus we need to hash incoming
        /// global mutex names so we can be sure they'll work regardless of the underlying
        /// implementation.
        /// </remarks>
        private string HashedName(string name)
        {
            using (var sha = SHA1.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(name)))
                    .Replace("-", "")
                    .ToLowerInvariant();
            }
        }

        public async ValueTask<IGlobalMutexReservation> ReserveExactAsync(string name, CancellationToken cancellationToken)
        {
            name = HashedName(name);

            if (OperatingSystem.IsWindows())
            {
                var semaphore = new Semaphore(1, 1, name);
                while (!semaphore.WaitOne(0))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
                return new WindowsGlobalMutexReservation(semaphore);
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                return new UnixGlobalMutexReservation(
                    await _reservationManager!.ReserveExactAsync(name, cancellationToken));
            }

            throw new PlatformNotSupportedException();
        }

        public async ValueTask<IGlobalMutexReservation?> TryReserveExactAsync(string name)
        {
            name = HashedName(name);

            if (OperatingSystem.IsWindows())
            {
                var semaphore = new Semaphore(1, 1, name);
                if (!semaphore.WaitOne(0))
                {
                    return null;
                }
                return new WindowsGlobalMutexReservation(semaphore);
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
            {
                var reservation = await _reservationManager!.TryReserveExactAsync(name);
                if (reservation == null)
                {
                    return null;
                }
                return new UnixGlobalMutexReservation(reservation);
            }

            throw new PlatformNotSupportedException();
        }
    }
}
