namespace UET.Commands.OpenGE
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using Redpoint.Registry;
    using Redpoint.ServiceControl;
    using Redpoint.Uet.OpenGE;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Reflection;
    using System.Threading.Tasks;
    using UET.Services;

    internal sealed class OpenGEInstallCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateInstallCommand()
        {
            var options = new Options();
            var command = new Command("install", "Install or upgrade the OpenGE system-wide agent on this machine. This will allow you to use OpenGE from Visual Studio.");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGEInstallCommandInstance>(
                options,
                services =>
                {
                });
            return command;
        }

        private sealed class OpenGEInstallCommandInstance : ICommandInstance
        {
            private readonly ILogger<OpenGEInstallCommandInstance> _logger;
            private readonly IServiceControl _serviceControl;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;
            private readonly ISelfLocation _selfLocation;

            public OpenGEInstallCommandInstance(
                ILogger<OpenGEInstallCommandInstance> logger,
                IServiceControl serviceControl,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory,
                ISelfLocation selfLocation)
            {
                _logger = logger;
                _serviceControl = serviceControl;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
                _selfLocation = selfLocation;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!_serviceControl.HasPermissionToInstall)
                {
                    _logger.LogError("This command must be run as an Administrator / root.");
                    return 1;
                }

                var (shimVersionFolder, version, basePath, uetPath) = await GetUetVersion().ConfigureAwait(false);
                if (!File.Exists(uetPath))
                {
                    _logger.LogError($"Expected UET to be installed globally at '{uetPath}'. Maybe you need to run 'uet upgrade' first?");
                }
                await ExtractXgConsoleShim(basePath).ConfigureAwait(false);
                var agentPath = await DownloadOpenGEAgent(version, basePath).ConfigureAwait(false);
                await InstallOpenGEAgent(agentPath).ConfigureAwait(false);
                SetUpXGERegistryDummyValues();

                _logger.LogInformation("The OpenGE agent has been installed and started.");
                return 0;
            }

            private void SetUpXGERegistryDummyValues()
            {
                // Set 'SOFTWARE\Xoreax\IncrediBuild\BuildService' value 'CoordHost' to '127.0.0.1'.
                if (OperatingSystem.IsWindows())
                {
                    _logger.LogInformation("Set Incredibuild coordinator to 127.0.0.1 for UBT tooling...");
                    var stack = RegistryStack.OpenPath(@"HKCU:\SOFTWARE\Xoreax\IncrediBuild\BuildService", true, true);
                    stack.Key.SetValue("CoordHost", "127.0.0.1");
                }
            }

            private async Task<string> DownloadOpenGEAgent(string version, string basePath)
            {
                string downloadUrl;
                string filename;
                if (OperatingSystem.IsWindows())
                {
                    downloadUrl = $"https://github.com/RedpointGames/uet/releases/download/{version}/openge-agent.exe";
                    filename = "openge-agent.exe";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    downloadUrl = $"https://github.com/RedpointGames/uet/releases/download/{version}/openge-agent";
                    filename = "openge-agent";
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                if (!File.Exists(Path.Combine(basePath, filename)))
                {
                    _logger.LogInformation($"Downloading OpenGE agent for {version}...");
                    using (var client = new HttpClient())
                    {
                        using (var target = new FileStream(Path.Combine(basePath, filename + ".tmp"), FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var response = await client.GetAsync(new Uri(downloadUrl), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                            response.EnsureSuccessStatusCode();
                            using (var stream = new PositionAwareStream(
                                await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                                response.Content.Headers.ContentLength!.Value))
                            {
                                var cts = new CancellationTokenSource();
                                var progress = _progressFactory.CreateProgressForStream(stream);
                                var monitorTask = Task.Run(async () =>
                                {
                                    var monitor = _monitorFactory.CreateByteBasedMonitor();
                                    await monitor.MonitorAsync(
                                        progress,
                                        SystemConsole.ConsoleInformation,
                                        SystemConsole.WriteProgressToConsole,
                                        cts.Token).ConfigureAwait(false);
                                });

                                await stream.CopyToAsync(target).ConfigureAwait(false);

                                await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts).ConfigureAwait(false);
                            }
                        }
                    }

                    File.Move(Path.Combine(basePath, filename + ".tmp"), Path.Combine(basePath, filename), true);
                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(
                            Path.Combine(basePath, filename),
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute | UnixFileMode.OtherRead);
                    }

                    _logger.LogInformation($"OpenGE agent {version} has been downloaded successfully.");
                }

                return Path.Combine(basePath, filename);
            }

            private async Task InstallOpenGEAgent(string agentPath)
            {
                // Re-install OpenGE agent.
                var daemonName = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => "Incredibuild Agent",
                    var v when v == OperatingSystem.IsMacOS() => "openge-agent",
                    var v when v == OperatingSystem.IsLinux() => "openge-agent",
                    _ => throw new PlatformNotSupportedException(),
                };
                if (await _serviceControl.IsServiceInstalled(daemonName).ConfigureAwait(false))
                {
                    if (await _serviceControl.IsServiceRunning(daemonName).ConfigureAwait(false))
                    {
                        _logger.LogInformation("Stopping OpenGE agent...");
                        await _serviceControl.StopService(daemonName).ConfigureAwait(false);
                    }

                    _logger.LogInformation("Uninstalling OpenGE agent...");
                    await _serviceControl.UninstallService(daemonName).ConfigureAwait(false);
                }
                _logger.LogInformation("Installing OpenGE agent...");
                if (OperatingSystem.IsMacOS())
                {
                    Directory.CreateDirectory("/Users/Shared/OpenGE");
                }
                await _serviceControl.InstallService(
                    daemonName,
                    "The OpenGE agent provides remote compilation services.",
                    $@"{agentPath} --service",
                    OperatingSystem.IsMacOS() ? "/Users/Shared/OpenGE/stdout.log" : null,
                    OperatingSystem.IsMacOS() ? "/Users/Shared/OpenGE/stderr.log" : null).ConfigureAwait(false);

                _logger.LogInformation("Starting OpenGE agent...");
                await _serviceControl.StartService(daemonName).ConfigureAwait(false);
            }

            private async Task ExtractXgConsoleShim(string basePath)
            {
                // Extract xgConsole shim.
                var shimName = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => "xgConsole.exe",
                    var v when v == OperatingSystem.IsMacOS() => "xgConsole",
                    var v when v == OperatingSystem.IsLinux() => "ib_console",
                    _ => throw new PlatformNotSupportedException(),
                };
                var embeddedResourceName = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => "win_x64.xgConsole.exe",
                    var v when v == OperatingSystem.IsMacOS() => "osx_arm64.xgConsole",
                    var v when v == OperatingSystem.IsLinux() => "linux_x64.ib_console",
                    _ => throw new PlatformNotSupportedException(),
                };
                var xgeShimPath = Path.Combine(basePath, shimName);
                if (!File.Exists(xgeShimPath))
                {
                    var manifestName = $"{typeof(IOpenGEProvider).Namespace}.Embedded.{embeddedResourceName}";
                    var manifestStream = typeof(IOpenGEProvider).Assembly.GetManifestResourceStream(manifestName);
                    if (manifestStream == null)
                    {
                        throw new InvalidOperationException($"This process requires the OpenGE shim to be extracted, but UET was incorrectly built and doesn't have a copy of the shim as an embedded resource with the name '{manifestName}'.");
                    }
                    using (manifestStream)
                    {
                        using (var target = new FileStream(xgeShimPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await manifestStream!.CopyToAsync(target).ConfigureAwait(false);
                        }
                    }
                    if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                    {
                        var mode = File.GetUnixFileMode(xgeShimPath + ".tmp");
                        mode |= UnixFileMode.UserExecute;
                        mode |= UnixFileMode.GroupExecute;
                        mode |= UnixFileMode.OtherExecute;
                        File.SetUnixFileMode(xgeShimPath + ".tmp", mode);
                    }
                    File.Move(xgeShimPath + ".tmp", xgeShimPath, true);
                    _logger.LogTrace("Extracted XGE shim to: " + xgeShimPath);
                }
            }

            private async Task<(string shimVersionFolder, string version, string basePath, string uetPath)> GetUetVersion()
            {
                // Get the current version.
                var currentVersionAttributeValue = RedpointSelfVersion.GetInformationalVersion();
                if (currentVersionAttributeValue != null && !currentVersionAttributeValue.EndsWith("-pre", StringComparison.Ordinal))
                {
                    var version = currentVersionAttributeValue;
                    var basePath = true switch
                    {
                        var v when v == OperatingSystem.IsWindows() => Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "UET",
                            version),
                        var v when v == OperatingSystem.IsMacOS() => $"/Users/Shared/UET/{version}",
                        _ => throw new PlatformNotSupportedException()
                    };
                    var uetName = true switch
                    {
                        var v when v == OperatingSystem.IsWindows() => "uet.exe",
                        _ => "uet",
                    };
                    var uetPath = Path.Combine(basePath, uetName);
                    return (version, version, basePath, uetPath);
                }
                else
                {
                    _logger.LogWarning("Unable to auto-detect running UET version; the xgConsole shim will be installed into the Current folder, even if the versions don't match.");
                    var shimVersionFolder = "Current";
                    var basePath = true switch
                    {
                        var v when v == OperatingSystem.IsWindows() => Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "UET",
                            shimVersionFolder),
                        var v when v == OperatingSystem.IsMacOS() => $"/Users/Shared/UET/{shimVersionFolder}",
                        _ => throw new PlatformNotSupportedException()
                    };
                    var uetPath = _selfLocation.GetUETLocalLocation();

                    string version;
                    const string latestUrl = "https://github.com/RedpointGames/uet/releases/download/latest/package.version";
                    _logger.LogInformation("Checking for the latest version...");
                    using (var client = new HttpClient())
                    {
                        version = (await client.GetStringAsync(new Uri(latestUrl)).ConfigureAwait(false)).Trim();
                    }

                    if (string.IsNullOrWhiteSpace(version))
                    {
                        throw new InvalidOperationException("Unable to determine latest version!");
                    }

                    return (shimVersionFolder, version, basePath, uetPath);
                }
            }
        }
    }
}
