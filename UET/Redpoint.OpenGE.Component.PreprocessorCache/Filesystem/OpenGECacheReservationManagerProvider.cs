namespace Redpoint.OpenGE.Component.PreprocessorCache.Filesystem
{
    using Redpoint.Reservation;

    internal class OpenGECacheReservationManagerProvider : IOpenGECacheReservationManagerProvider
    {
        private readonly IReservationManager _reservationManager;

        public OpenGECacheReservationManagerProvider(
            IReservationManagerFactory reservationManagerFactory)
        {
            var dataDirectory = true switch
            {
                var v when v == OperatingSystem.IsWindows() => Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "OpenGE",
                    "Cache"),
                var v when v == OperatingSystem.IsMacOS() => Path.Combine("/Users", "Shared", "OpenGE", "Cache"),
                var v when v == OperatingSystem.IsLinux() => Path.Combine("/tmp", "OpenGE", "Cache"),
                _ => throw new PlatformNotSupportedException(),
            };
            _reservationManager = reservationManagerFactory.CreateReservationManager(dataDirectory);
        }

        public IReservationManager ReservationManager => _reservationManager;
    }
}
