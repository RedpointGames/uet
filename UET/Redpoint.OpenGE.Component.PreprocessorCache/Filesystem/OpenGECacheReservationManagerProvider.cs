namespace Redpoint.OpenGE.Component.PreprocessorCache.Filesystem
{
    using Redpoint.Reservation;
    using System.Runtime.Versioning;

    internal class OpenGECacheReservationManagerProvider : IOpenGECacheReservationManagerProvider
    {
        private readonly IReservationManager _reservationManager;

        [SupportedOSPlatform("windows")]
        private static bool IsSystem()
        {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                return identity.IsSystem;
            }
        }

        public OpenGECacheReservationManagerProvider(
            IReservationManagerFactory reservationManagerFactory)
        {
#pragma warning disable CA1416
            var dataDirectory = true switch
            {
                var v when v == OperatingSystem.IsWindows() => Path.Combine(
                    Environment.GetFolderPath(IsSystem() 
                        ? Environment.SpecialFolder.CommonApplicationData 
                        : Environment.SpecialFolder.LocalApplicationData),
                    "OpenGE",
                    "Cache"),
                var v when v == OperatingSystem.IsMacOS() => Path.Combine("/Users", "Shared", "OpenGE", "Cache"),
                var v when v == OperatingSystem.IsLinux() => Path.Combine("/tmp", "OpenGE", "Cache"),
                _ => throw new PlatformNotSupportedException(),
            };
#pragma warning restore CA1416
            _reservationManager = reservationManagerFactory.CreateReservationManager(dataDirectory);
        }

        public IReservationManager ReservationManager => _reservationManager;
    }
}
