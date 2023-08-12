namespace Redpoint.OpenGE.Core
{
    using Redpoint.Reservation;
    using System.Runtime.Versioning;

    internal class ReservationManagerForOpenGE : IReservationManagerForOpenGE
    {
        private readonly string _rootDirectory;
        private readonly IReservationManager _reservationManager;

        [SupportedOSPlatform("windows")]
        private static bool IsSystem()
        {
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                return identity.IsSystem;
            }
        }

        public string RootDirectory => _rootDirectory;

        public ReservationManagerForOpenGE(
            IReservationManagerFactory reservationManagerFactory)
        {
#pragma warning disable CA1416
            _rootDirectory = true switch
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
            _reservationManager = reservationManagerFactory.CreateReservationManager(_rootDirectory);
        }

        public IReservationManager ReservationManager => _reservationManager;
    }
}
