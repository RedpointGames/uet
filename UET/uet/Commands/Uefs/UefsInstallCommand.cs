namespace UET.Commands.Uefs
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using Redpoint.ServiceControl;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Reflection;
    using System.Threading.Tasks;

    internal class UefsInstallCommand
    {
        internal class Options
        {
        }

        public static Command CreateInstallCommand()
        {
            var options = new Options();
            var command = new Command("install", "Install or upgrade the UEFS daemon on this machine.");
            command.AddAllOptions(options);
            command.AddCommonHandler<UefsInstallCommandInstance>(
                options,
                services =>
                {
                });
            return command;
        }

        private class UefsInstallCommandInstance : ICommandInstance
        {
            private readonly ILogger<UefsInstallCommandInstance> _logger;
            private readonly IServiceControl _serviceControl;
            private readonly IProgressFactory _progressFactory;
            private readonly IMonitorFactory _monitorFactory;

            public UefsInstallCommandInstance(
                ILogger<UefsInstallCommandInstance> logger,
                IServiceControl serviceControl,
                IProgressFactory progressFactory,
                IMonitorFactory monitorFactory)
            {
                _logger = logger;
                _serviceControl = serviceControl;
                _progressFactory = progressFactory;
                _monitorFactory = monitorFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!_serviceControl.HasPermissionToInstall)
                {
                    _logger.LogError("This command must be run as an Administrator / root.");
                    return 1;
                }

                string version;
                var currentVersionAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (currentVersionAttribute != null && !currentVersionAttribute.InformationalVersion.EndsWith("-pre"))
                {
                    version = currentVersionAttribute.InformationalVersion;
                }
                else
                {
                    const string latestUrl = "https://github.com/RedpointGames/uet/releases/download/latest/package.version";

                    _logger.LogInformation("Checking for the latest version...");
                    using (var client = new HttpClient())
                    {
                        version = (await client.GetStringAsync(latestUrl).ConfigureAwait(false)).Trim();
                    }

                    if (string.IsNullOrWhiteSpace(version))
                    {
                        _logger.LogError("Could not fetch latest version.");
                        return 1;
                    }
                }

                string downloadUrl;
                string baseFolder;
                string filename;
                string daemonName;
                string? stdoutPath;
                string? stderrPath;
                if (OperatingSystem.IsWindows())
                {
                    downloadUrl = $"https://github.com/RedpointGames/uet/releases/download/{version}/uefs-daemon.exe";
                    baseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "UET");
                    filename = "uefs-daemon.exe";
                    daemonName = "UEFS Service";
                    stdoutPath = null;
                    stderrPath = null;
                }
                else if (OperatingSystem.IsMacOS())
                {
                    downloadUrl = $"https://github.com/RedpointGames/uet/releases/download/{version}/uefs-daemon";
                    baseFolder = "/Users/Shared/UET";
                    filename = "uefs-daemon";
                    daemonName = "games.redpoint.UEFS";
                    stdoutPath = "/Users/Shared/UEFS/logs/stdout.log";
                    stderrPath = "/Users/Shared/UEFS/logs/stderr.log";
                    Directory.CreateDirectory("/Users/Shared/UEFS/logs");
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                Directory.CreateDirectory(baseFolder);
                Directory.CreateDirectory(Path.Combine(baseFolder, version));

                if (!File.Exists(Path.Combine(baseFolder, version, filename)))
                {
                    _logger.LogInformation($"Downloading UEFS daemon for {version}...");
                    using (var client = new HttpClient())
                    {
                        using (var target = new FileStream(Path.Combine(baseFolder, version, filename + ".tmp"), FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
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

                    File.Move(Path.Combine(baseFolder, version, filename + ".tmp"), Path.Combine(baseFolder, version, filename), true);
                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(
                            Path.Combine(baseFolder, version, filename),
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute | UnixFileMode.OtherRead);
                    }

                    _logger.LogInformation($"UEFS daemon {version} has been downloaded successfully.");
                }

                if (await _serviceControl.IsServiceInstalled(daemonName).ConfigureAwait(false))
                {
                    if (await _serviceControl.IsServiceRunning(daemonName).ConfigureAwait(false))
                    {
                        _logger.LogInformation("Stopping UEFS daemon...");
                        await _serviceControl.StopService(daemonName).ConfigureAwait(false);
                    }

                    _logger.LogInformation("Uninstalling UEFS daemon...");
                    await _serviceControl.UninstallService(daemonName).ConfigureAwait(false);
                }

                _logger.LogInformation("Installing UEFS daemon...");
                await _serviceControl.InstallService(
                    daemonName,
                    "The UEFS daemon provides storage virtualization APIs.",
                    $"{Path.Combine(baseFolder, version, filename)} --service",
                    stdoutPath,
                    stderrPath).ConfigureAwait(false);

                _logger.LogInformation("Starting UEFS daemon...");
                await _serviceControl.StartService(daemonName).ConfigureAwait(false);

                _logger.LogInformation("The UEFS service has been started.");
                return 0;
            }
        }
    }
}
