namespace UET.Commands.Android
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Redpoint.Uet.SdkManagement;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Concurrency;
    using Redpoint.Uet.CommonPaths;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Workspace.Reservation;
    using Redpoint.Reservation;
    using Redpoint.ProcessExecution;
    using System.Globalization;
    using System.Text;
    using Redpoint.PathResolution;
    using Redpoint.Uet.Commands.ParameterSpec;
    using Redpoint.CommandLine;

    internal sealed class AndroidKeepWirelessEnabledCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<bool> Once;

            public Options()
            {
                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine which defines the Android SDK version to use.",
                    parseArgument: EngineSpec.ParseEngineSpecContextless(),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;

                Once = new Option<bool>("--once")
                {
                    Description = "If set, this command performs the action once instead of continously running in the background. This option should be used when scheduling this operation on a build server (rather than running as a background service).",
                };
                Once.AddAlias("-o");
            }
        }

        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<CreateAndroidKeepWirelessEnabledCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command("keep-wireless-enabled", "Automatically keep 'Wireless debugging' enabled on connected Android devices.");
                    builder.GlobalContext.CommandRequiresUetVersionInBuildConfig(command);
                    return command;
                })
            .Build();

        private sealed class CreateAndroidKeepWirelessEnabledCommandInstance : ICommandInstance
        {
            private readonly ILogger<CreateAndroidKeepWirelessEnabledCommandInstance> _logger;
            private readonly ILocalSdkManager _localSdkManager;
            private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
            private readonly IServiceProvider _serviceProvider;
            private readonly IProcessExecutor _processExecutor;
            private readonly IPathResolver _pathResolver;
            private readonly ILoopbackPortReservationManager _loopbackPortReservationManager;
            private readonly Options _options;

            public CreateAndroidKeepWirelessEnabledCommandInstance(
                ILogger<CreateAndroidKeepWirelessEnabledCommandInstance> logger,
                ILocalSdkManager localSdkManager,
                IEngineWorkspaceProvider engineWorkspaceProvider,
                IServiceProvider serviceProvider,
                IReservationManagerFactory reservationManagerFactory,
                IProcessExecutor processExecutor,
                IPathResolver pathResolver,
                Options options)
            {
                _logger = logger;
                _localSdkManager = localSdkManager;
                _engineWorkspaceProvider = engineWorkspaceProvider;
                _serviceProvider = serviceProvider;
                _processExecutor = processExecutor;
                _pathResolver = pathResolver;
                _loopbackPortReservationManager = reservationManagerFactory.CreateLoopbackPortReservationManager();
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                if (!OperatingSystem.IsWindows())
                {
                    _logger.LogError("This command is not currently supported on non-Windows platforms.");
                    return 1;
                }

                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                var once = context.ParseResult.GetValueForOption(_options.Once)!;

                var engineSpec = engine.ToBuildEngineSpecification("keep-wireless-enabled");

                await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                    engineSpec,
                    string.Empty,
                    context.GetCancellationToken()).ConfigureAwait(false))
                        .AsAsyncDisposable(out var engineWorkspace)
                        .ConfigureAwait(false))
                {
                    var packagePath = UetPaths.UetDefaultWindowsSdkStoragePath;
                    Directory.CreateDirectory(packagePath);
                    var envVars = await _localSdkManager.SetupEnvironmentForSdkSetups(
                        engineWorkspace.Path,
                        packagePath,
                        _serviceProvider.GetServices<ISdkSetup>().Where(x => x.PlatformNames.Contains("Android")).ToHashSet(),
                        context.GetCancellationToken()).ConfigureAwait(false);

                    await using ((await _loopbackPortReservationManager.ReserveAsync().ConfigureAwait(false))
                        .AsAsyncDisposable(out var loopbackPort)
                        .ConfigureAwait(false))
                    {
                        var adbPath = Path.Combine(
                            envVars["ANDROID_HOME"],
                            "platform-tools",
                            "adb.exe");

                        try
                        {
                            do
                            {
                                _logger.LogInformation($"Listing devices...");
                                var devicesStringBuilder = new StringBuilder();
                                await _processExecutor.ExecuteAsync(
                                    new ProcessSpecification
                                    {
                                        FilePath = adbPath,
                                        Arguments = ["devices", "-l"],
                                        EnvironmentVariables = envVars,
                                    },
                                    CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(devicesStringBuilder),
                                    context.GetCancellationToken());
                                var devicesOutput = devicesStringBuilder.ToString()
                                    .Replace("List of devices attached", "", StringComparison.OrdinalIgnoreCase)
                                    .Trim();
                                var devices = devicesOutput
                                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                                    .Split("\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                                _logger.LogInformation($"Found {devices.Length} devices.");
                                if (!string.IsNullOrWhiteSpace(devicesOutput))
                                {
                                    _logger.LogInformation(devicesOutput);
                                }
                                foreach (var deviceEntry in devices)
                                {
                                    var device = deviceEntry.Split("  ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                                    if (device.Length < 2)
                                    {
                                        continue;
                                    }

                                    var deviceId = device[0];
                                    var details = device[1];
                                    if (!deviceId.Contains(':', StringComparison.Ordinal) &&
                                        !deviceId.Contains("._tcp", StringComparison.Ordinal))
                                    {
                                        // This is a USB connected device.

                                        _logger.LogInformation($"Querying wlan0 IP address of '{deviceId}'...");
                                        var addressStringBuilder = new StringBuilder();
                                        await _processExecutor.ExecuteAsync(
                                            new ProcessSpecification
                                            {
                                                FilePath = adbPath,
                                                Arguments = ["-s", deviceId, "shell", "ip", "-o", "-4", "address", "show", "dev", "wlan0"],
                                                EnvironmentVariables = envVars,
                                            },
                                            CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(addressStringBuilder),
                                            context.GetCancellationToken());
                                        var addressMatch = new Regex("inet ([0-9\\.]+)/").Match(addressStringBuilder.ToString());
                                        if (!addressMatch.Success)
                                        {
                                            _logger.LogWarning($"Unable to find wlan0 address for {deviceId}:\n{addressStringBuilder}");
                                            continue;
                                        }

                                        var address = addressMatch.Groups[1].Value;
                                        _logger.LogInformation($"The device's wireless IP address is: {address}");

                                        // Check if we can already connect - we don't want to restart adbd if it's already in TCP/IP mode
                                        // because that may interfere with any builds that are currently using it.
                                        var needsConnection = false;
                                        _logger.LogInformation($"Attempting to connect to device on '{address}:5555'...");
                                        var connectStringBuilder = new StringBuilder();
                                        await _processExecutor.ExecuteAsync(
                                            new ProcessSpecification
                                            {
                                                FilePath = adbPath,
                                                Arguments = ["connect", $"{address}:5555"],
                                                EnvironmentVariables = envVars,
                                            },
                                            CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(connectStringBuilder),
                                            context.GetCancellationToken());
                                        _logger.LogInformation(connectStringBuilder.ToString());

                                        if (connectStringBuilder.ToString().Contains("cannot connect", StringComparison.OrdinalIgnoreCase))
                                        {
                                            needsConnection = true;
                                        }
                                        else if (connectStringBuilder.ToString().Contains("already connected", StringComparison.OrdinalIgnoreCase))
                                        {
                                            // Make sure the connection is actually usable.
                                            var testExitCode = await _processExecutor.ExecuteAsync(
                                                new ProcessSpecification
                                                {
                                                    FilePath = adbPath,
                                                    Arguments = ["-s", deviceId, "shell", "echo", "wifi connected"],
                                                    EnvironmentVariables = envVars,
                                                },
                                                CaptureSpecification.Passthrough,
                                                context.GetCancellationToken());
                                            if (testExitCode != 0)
                                            {
                                                needsConnection = true;
                                            }
                                        }

                                        if (needsConnection)
                                        {
                                            _logger.LogInformation($"Disconnecting from any device on '{address}:5555' if it's already in the device list...");
                                            await _processExecutor.ExecuteAsync(
                                                new ProcessSpecification
                                                {
                                                    FilePath = adbPath,
                                                    Arguments = ["disconnect", $"{address}:5555"],
                                                    EnvironmentVariables = envVars,
                                                },
                                                CaptureSpecification.Passthrough,
                                                context.GetCancellationToken());

                                            // We can't connect to the device on it's wireless IP address, so we need to switch to TCP/IP mode.
                                            _logger.LogInformation($"Enabling TCP/IP connection on '{deviceId}'...");
                                            await _processExecutor.ExecuteAsync(
                                                new ProcessSpecification
                                                {
                                                    FilePath = adbPath,
                                                    Arguments = ["-s", deviceId, "tcpip", "5555"],
                                                    EnvironmentVariables = envVars,
                                                },
                                                CaptureSpecification.Passthrough,
                                                context.GetCancellationToken());

                                            _logger.LogInformation($"Attempting to connect to device on '{address}:5555'...");
                                            connectStringBuilder = new StringBuilder();
                                            await _processExecutor.ExecuteAsync(
                                                new ProcessSpecification
                                                {
                                                    FilePath = adbPath,
                                                    Arguments = ["connect", $"{address}:5555"],
                                                    EnvironmentVariables = envVars,
                                                },
                                                CaptureSpecification.CreateFromSanitizedStdoutStringBuilder(connectStringBuilder),
                                                context.GetCancellationToken());
                                            _logger.LogInformation(connectStringBuilder.ToString());
                                            if (connectStringBuilder.ToString().Contains("cannot connect", StringComparison.OrdinalIgnoreCase))
                                            {
                                                _logger.LogError($"Device '{deviceId}' failed to switch into TCP/IP mode.");
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogInformation($"Device '{deviceId}' is already in TCP/IP mode.");
                                        }
                                    }
                                }

                                if (!once)
                                {
                                    await Task.Delay(2000, context.GetCancellationToken()).ConfigureAwait(false);
                                }
                            }
                            while (!once && !context.GetCancellationToken().IsCancellationRequested);
                        }
                        catch (OperationCanceledException) when (context.GetCancellationToken().IsCancellationRequested)
                        {
                            // Expected.
                        }
                    }
                }

                _logger.LogInformation("Android 'keep-wireless-enabled' command finished.");
                return 0;
            }
        }
    }
}
