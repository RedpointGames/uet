namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Core;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class DefaultLocalSdkManager : ILocalSdkManager
    {
        private readonly IReservationManagerFactory _reservationManagerFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DefaultLocalSdkManager> _logger;
        private ConcurrentDictionary<string, IReservationManager> _reservationManagers;

        public DefaultLocalSdkManager(
            IReservationManagerFactory reservationManagerFactory,
            IServiceProvider serviceProvider,
            ILogger<DefaultLocalSdkManager> logger)
        {
            _reservationManagers = new ConcurrentDictionary<string, IReservationManager>(StringComparer.InvariantCultureIgnoreCase);
            _reservationManagerFactory = reservationManagerFactory;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public IEnumerable<string> GetRecognisedPlatforms()
        {
            yield return "Windows";
            yield return "Win64";
            yield return "Mac";
            yield return "Android";
            yield return "Linux";

            // Allow non-portable platform support to be added via environment
            // variables.
            if (OperatingSystem.IsWindows())
            {
                foreach (var envvar in Environment.GetEnvironmentVariables()
                    .Keys
                    .OfType<string>())
                {
                    if (envvar.StartsWith("UET_PLATFORM_SDK_CONFIG_PATH_"))
                    {
                        yield return envvar.Substring("UET_PLATFORM_SDK_CONFIG_PATH_".Length);
                    }
                }
            }
        }

        public async Task<Dictionary<string, string>> EnsureSdkForPlatformAsync(
            string enginePath,
            string sdksPath,
            string platform,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Requesting SDK for platform {platform}...");

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
                        _logger.LogInformation("Using Windows SDK setup provider.");
                        setup = _serviceProvider.GetRequiredService<WindowsSdkSetup>();
                        platform = "Windows";
                    }
                    break;
                case "Mac":
                    if (OperatingSystem.IsMacOS())
                    {
                        _logger.LogInformation("Using macOS SDK setup provider.");
                        setup = _serviceProvider.GetRequiredService<MacSdkSetup>();
                    }
                    break;
                case "Android":
                    if (OperatingSystem.IsWindows())
                    {
                        _logger.LogInformation("Using Android SDK setup provider.");
                        setup = _serviceProvider.GetRequiredService<AndroidSdkSetup>();
                    }
                    break;
                case "Linux":
                    if (OperatingSystem.IsWindows())
                    {
                        _logger.LogInformation("Using Linux SDK setup provider.");
                        setup = _serviceProvider.GetRequiredService<LinuxSdkSetup>();
                    }
                    break;
                default:
                    if (OperatingSystem.IsWindows())
                    {
                        var configPath = Environment.GetEnvironmentVariable($"UET_PLATFORM_SDK_CONFIG_PATH_{platform}");
                        if (configPath != null)
                        {
                            _logger.LogInformation($"Using {platform} confidential SDK setup provider from: {configPath}");
                            var config = JsonSerializer.Deserialize(
                                File.ReadAllText(configPath),
                                ConfidentialPlatformJsonSerializerContext.Default.ConfidentialPlatformConfig);
                            setup = new ConfidentialSdkSetup(
                                platform,
                                config!,
                                _serviceProvider.GetRequiredService<IProcessExecutor>(),
                                _serviceProvider.GetRequiredService<ILogger<ConfidentialSdkSetup>>(),
                                _serviceProvider.GetRequiredService<IStringUtilities>());
                        }
                    }
                    break;
            }
            if (setup == null)
            {
                _logger.LogWarning($"The platform {platform} has no automatic SDK setup provider. The necessary dependencies and environment for the build must already be installed on this machine.");
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

            if (env.EnvironmentVariables.Count == 0)
            {
                _logger.LogInformation($"The {platform} SDK setup did not provide any environment variables for the build.");
            }
            else
            {
                _logger.LogInformation($"The {platform} SDK setup provided the following environment variables:");
                foreach (var kv in env.EnvironmentVariables)
                {
                    _logger.LogInformation($"  {kv.Key}={kv.Value}");
                }
            }

            return env.EnvironmentVariables;
        }
    }
}
