namespace Redpoint.UET.SdkManagement
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Reservation;
    using Redpoint.UET.Core;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal class DefaultLocalSdkManager : ILocalSdkManager
    {
        private readonly IReservationManagerFactory _reservationManagerFactory;
        private readonly IServiceProvider _serviceProvider;
        private ConcurrentDictionary<string, IReservationManager> _reservationManagers;

        public DefaultLocalSdkManager(
            IReservationManagerFactory reservationManagerFactory,
            IServiceProvider serviceProvider)
        {
            _reservationManagers = new ConcurrentDictionary<string, IReservationManager>(StringComparer.InvariantCultureIgnoreCase);
            _reservationManagerFactory = reservationManagerFactory;
            _serviceProvider = serviceProvider;
        }

        public string[] GetRecognisedPlatforms()
        {
            return new[] { "Windows", "Win64", "Mac", "Android", "Linux" };
        }

        public async Task<Dictionary<string, string>> EnsureSdkForPlatformAsync(
            string enginePath,
            string sdksPath,
            string platform,
            CancellationToken cancellationToken)
        {
            var reservationManager = _reservationManagers.GetOrAdd(
                sdksPath.TrimEnd(new[] { '\\', '/' }),
                _reservationManagerFactory.CreateReservationManager);

            ISdkSetup? setup = null;
            switch (platform)
            {
                case "Windows":
                case "Win64":
                    if (OperatingSystem.IsWindows())
                    {
                        setup = _serviceProvider.GetRequiredService<WindowsSdkSetup>();
                        platform = "Windows";
                    }
                    break;
                case "Mac":
                    if (OperatingSystem.IsMacOS())
                    {
                        setup = _serviceProvider.GetRequiredService<MacSdkSetup>();
                    }
                    break;
                case "Android":
                    if (OperatingSystem.IsWindows())
                    {
                        setup = _serviceProvider.GetRequiredService<AndroidSdkSetup>();
                    }
                    break;
                case "Linux":
                    if (OperatingSystem.IsWindows())
                    {
                        setup = _serviceProvider.GetRequiredService<LinuxSdkSetup>();
                    }
                    break;
            }
            if (setup == null)
            {
                return new Dictionary<string, string>();
            }

            EnvironmentForSdkUsage env;

            var packageId = $"{platform}-{await setup.ComputeSdkPackageId(enginePath, cancellationToken)}";
            await using (var reservation = await reservationManager.ReserveExactAsync(packageId, cancellationToken))
            {
                if (!File.Exists(Path.Combine(reservation.ReservedPath, "sdk-ready")))
                {
                    var packageWorkingPath = Path.Combine(sdksPath, $"{packageId}-tmp-{Process.GetCurrentProcess().Id}");
                    var packageOldPath = Path.Combine(sdksPath, $"{packageId}-old-{Process.GetCurrentProcess().Id}");
                    if (Directory.Exists(packageWorkingPath))
                    {
                        await DirectoryAsync.DeleteAsync(packageWorkingPath, true);
                    }
                    if (Directory.Exists(packageOldPath))
                    {
                        await DirectoryAsync.DeleteAsync(packageOldPath, true);
                    }
                    Directory.CreateDirectory(packageWorkingPath);
                    await setup.GenerateSdkPackage(enginePath, packageWorkingPath, cancellationToken);
                    await File.WriteAllTextAsync(Path.Combine(packageWorkingPath, "sdk-ready"), "ready", cancellationToken);
                    try
                    {
                        if (Directory.Exists(reservation.ReservedPath))
                        {
                            await DirectoryAsync.MoveAsync(reservation.ReservedPath, packageOldPath);
                        }
                        await DirectoryAsync.MoveAsync(packageWorkingPath, reservation.ReservedPath);
                    }
                    catch
                    {
                        if (!Directory.Exists(reservation.ReservedPath) &&
                            Directory.Exists(packageOldPath))
                        {
                            await DirectoryAsync.MoveAsync(packageOldPath, reservation.ReservedPath);
                        }
                    }
                    finally
                    {
                        if (Directory.Exists(packageOldPath))
                        {
                            await DirectoryAsync.DeleteAsync(packageOldPath, true);
                        }
                    }
                }

                env = await setup.EnsureSdkPackage(reservation.ReservedPath, cancellationToken);
            }

            return env.EnvironmentVariables;
        }
    }
}
