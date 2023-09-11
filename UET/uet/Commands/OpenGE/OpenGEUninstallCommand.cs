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

    internal class OpenGEUninstallCommand
    {
        internal class Options
        {
        }

        public static Command CreateUninstallCommand()
        {
            var options = new Options();
            var command = new Command("uninstall", "Uninstalls the OpenGE system-wide agent.");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGEUninstallCommandInstance>(
                options,
                services =>
                {
                });
            return command;
        }

        private class OpenGEUninstallCommandInstance : ICommandInstance
        {
            private readonly ILogger<OpenGEUninstallCommandInstance> _logger;
            private readonly IServiceControl _serviceControl;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;
            private readonly ISelfLocation _selfLocation;

            public OpenGEUninstallCommandInstance(
                ILogger<OpenGEUninstallCommandInstance> logger,
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

                var (_, _, basePath, _) = await GetUetVersion().ConfigureAwait(false);
                if (Directory.Exists(basePath))
                {
                    DeleteXgConsoleShim(basePath);
                }
                await UninstallOpenGEAgent().ConfigureAwait(false);

                _logger.LogInformation("The OpenGE agent has been uninstalled.");
                return 0;
            }

            private async Task UninstallOpenGEAgent()
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
            }

            private void DeleteXgConsoleShim(string basePath)
            {
                // Extract xgConsole shim.
                var shimName = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => "xgConsole.exe",
                    var v when v == OperatingSystem.IsMacOS() => "xgConsole",
                    var v when v == OperatingSystem.IsLinux() => "ib_console",
                    _ => throw new PlatformNotSupportedException(),
                };
                var xgeShimPath = Path.Combine(basePath, shimName);
                if (File.Exists(xgeShimPath))
                {
                    File.Delete(xgeShimPath);
                    _logger.LogTrace("Deleted XGE shim from: " + xgeShimPath);
                }
            }

            private async Task<(string shimVersionFolder, string version, string basePath, string uetPath)> GetUetVersion()
            {
                // Get the current version.
                var currentVersionAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (currentVersionAttribute != null && !currentVersionAttribute.InformationalVersion.EndsWith("-pre"))
                {
                    var version = currentVersionAttribute.InformationalVersion;
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
                        version = (await client.GetStringAsync(latestUrl).ConfigureAwait(false)).Trim();
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
