namespace UET.Commands.Android
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using UET.Commands.EngineSpec;
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

    internal sealed class AndroidKeepWirelessEnabledCommand
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

        public static Command CreateAndroidKeepWirelessEnabledCommand()
        {
            var options = new Options();
            var command = new Command("keep-wireless-enabled", "Automatically keep 'Wireless debugging' enabled on connected Android devices.");
            command.AddAllOptions(options);
            command.AddCommonHandler<CreateAndroidKeepWirelessEnabledCommandInstance>(options);
            return command;
        }

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

            public async Task<int> ExecuteAsync(InvocationContext context)
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
                        engine.Path!,
                        packagePath,
                        _serviceProvider.GetServices<ISdkSetup>().ToHashSet(),
                        context.GetCancellationToken()).ConfigureAwait(false);

                    var adbkeyPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".android",
                        "adbkey");
                    var adbkeyPubPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".android",
                        "adbkey.pub");

                    // @note: This is necessary on build servers where users and machines might vary over time.
                    var privateKey =
                        """
                        -----BEGIN PRIVATE KEY-----
                        MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDxzwYvn11zEXM4
                        yuWLzGp+C0XeQkUU6j3sJsK0bw7LRjSyR1vjAkoUJP1WMI5fy7OzzBPsg81YcODg
                        Y+DnYpaJLcG8R0GWTsZbMTG29VVraRcmDz7IUyLhzn7dA5Hc9+4gGZLfg5YmO0Dd
                        MViPAnJ0gdhqYbH4t9m3l1v5fDb+i3BAUKc5pSGw5jg4mRDWNj+vreWKHWx1i4bR
                        sOJbxKsTe9PXznTHwRQwtkazxPR994GPIe/lYOxYaYQqqgR0nhdH5nMJgPDPeSOD
                        bq9YeCPRw9yYUMa+n7cnSJ2ZMndMqiqHiYkDKGeuH2lCvsf8IT2QZD01+JEXbEKD
                        hK5zH/8dAgMBAAECggEABY5B4GC7OyxUtyKURRLAhJ5aL9nab/lUzGL0mMQvdRSr
                        G8h/cDcKgC18Y5lQgBt0SMaA06+QjX5kcEtjLLXLayHxwFNrypoLPSejcoZu/L2I
                        ol95zAz68XC24fmVxZutrS+hPADwN3cnjZ13YSvHeO1NzV3qwqHovurbml8T/WOn
                        1P+ISB4jtBuLeoRMfDKSZZDfljnaS+fk1ExRWFBMdbd2EqXQoII74oXjUL+6WblP
                        ZAMUduB02cLxaPwndQ8UYcxR/PGq+hPZza8k8yfJ8MXklkl/QtxOcljOe/Om7+5R
                        DKMD6dB6Jmd9QlfcJ+8U53/F4VXxYXja0YQBjJOV2QKBgQD49GZhaG+MsLKtepfD
                        leuq1hg+Rwu83yu7hyO5WKOHu82RZQjvizNSnxKx0NXv0L32cVLP0G3zDXI98lmk
                        C1/r7ExUOC2ECV95yOrXPjkJoFN3lkxKd0myxalUypfADtXqRMPtfjEnwcpGPwig
                        wsdG+lA5h7r+ajfT1RjIOkfV9QKBgQD4ptqKkBCBtF/8eMMIkNODL/3C9SCktHob
                        QCfOlZ2K4QYQyRK+2+yD2jBhpWxjK7BHhfK/WcawUcJT9f9KD3f8B4mqUADEweER
                        eWh90O3yRYd0k35V4SVBbuIAYRdwzHiS5WFVKYQyuYRUpRP1h5pXZWGarYxLqtO6
                        vhe7G/YjiQKBgDUFqIB6g7eNMqDsCUKovYanDobFDuTtCx1njN4+2KViBEhBIoQS
                        O54PLyYb+lSXOr4wKJkGJUSsynYTFbBwk79llmQhiuAiNulzN0EciX1ZXi2MHzeE
                        7HdczdG3TFalUj4Q40HDrKhxB6mqZyYGFfcx/MAj/lmNOdKuAhczAnW5AoGAQyet
                        NmcaTi2NDv7+jb2vomq/unvByToFEH8PQTgfSHbl0Hq92VZEVogDMRwgXdhaz7ZZ
                        jVyN0OkD9vEldbcfzK2sfJcG3h0O0E1d7z0SRrCImO+M21znVvi/iSKv1gMjPWk+
                        FGYWEi0QlFvRPCrXgGsdJU1h6r3EWVclyZ8PpyECgYEA87IZZCYXgvsGUz9sQSth
                        OQKEBE0OWnDHs1ZrMs0lfxKImPWrbj7CdKO5QlJADMwyH6nEL3ZVb8ETDK6T1VZa
                        Fbs6Pe7QRnoJucsM01pC79w0UtnNCHdXEEVeAbdKJmYgbGYU0Uvcsnz3M45lHsCy
                        T758Kl3IJGbsGU3C/T6WQUM=
                        -----END PRIVATE KEY-----
                        """.Replace("\r\n", "\n", StringComparison.Ordinal);
                    var publicKey =
                        """
                        QAAAAMtEwZUd/x9zroSDQmwXkfg1PWSQPSH8x75CaR+uZygDiYmHKqpMdzKZnUgnt5++xlCY3MPRI3hYr26DI3nP8IAJc+ZHF550BKoqhGlY7GDl7yGPgfd99MSzRrYwFMHHdM7X03sTq8Rb4rDRhot1bB2K5a2vPzbWEJk4OOawIaU5p1BAcIv+Nnz5W5e32bf4sWFq2IF0cgKPWDHdQDsmloPfkhkg7vfckQPdfs7hIlPIPg8mF2lrVfW2MTFbxk6WQUe8wS2JlmLn4GPg4HBYzYPsE8yzs8tfjjBW/SQUSgLjW0eyNEbLDm+0wibsPeoURULeRQt+asyL5co4cxFzXZ8vBs/xr6bxkIoKf8rjRNqozj5iXXPhLiW0euQNbFyxwMaGU2j/5phWQ3M5jyUWhsJ4iZb1+m8qyIqgjxfVnnBQlJZdH9ytkERWRx905AE2ITStDL5JUFYrl7hlNgArhdhUosBpVbl/a1DbP4EDaqeFhgfuCy77/I0SoAm5XgMnfqIv3w+tBR8kPGh1sJKxkICXqGK5Zhiyag3BIfLz3eskB1XZdrFn8JrvRsj3ZQwpxDGTtlw2lsq3FbKlVWMucyqbNTnBiuI/Vd10HqY6oaLvGKMbbwnr8eQS3x/0T0vn0b30KEibLAjPwaomuFWq6UdvIGY1RWlEqS93PUtPpIki3vqQkQEAAQA= uet-well-known-key
                        """.Replace("\r\n", "\n", StringComparison.Ordinal);

                    var existingPrivateKey = File.Exists(adbkeyPath) ? File.ReadAllText(adbkeyPath) : string.Empty;
                    var existingPublicKey = File.Exists(adbkeyPubPath) ? File.ReadAllText(adbkeyPubPath) : string.Empty;

                    if (existingPrivateKey != privateKey || existingPublicKey != publicKey)
                    {
                        _logger.LogInformation("Forcing adbkey to be a well-known key to avoid USB re-authorization prompts...");
                        File.WriteAllText(adbkeyPath, privateKey);
                        File.WriteAllText(adbkeyPubPath, publicKey);

                        _logger.LogInformation("Terminating any existing 'adb' processes to ensure ADB server sees new public/private keypair...");
                        await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = await _pathResolver.ResolveBinaryPath("taskkill").ConfigureAwait(false),
                                Arguments = ["/f", "/im", "adb.exe"]
                            },
                            CaptureSpecification.Passthrough,
                            CancellationToken.None).ConfigureAwait(false);
                    }

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
