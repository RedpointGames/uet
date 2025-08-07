namespace Redpoint.ServiceControl
{
    using Redpoint.ProcessExecution;
    using Redpoint.ProcessExecution.Enumerable;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    [SupportedOSPlatform("linux")]
    public sealed class LinuxServiceControl : IServiceControl
    {
        private readonly IProcessExecutor? _processExecutor;

        public LinuxServiceControl(
            IProcessExecutor? processExecutor = null)
        {
            _processExecutor = processExecutor;
        }

        [DllImport("libc")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern uint geteuid();

        public bool HasPermissionToInstall => geteuid() == 0;

        public bool HasPermissionToStart => geteuid() == 0;

        public Task<bool> IsServiceInstalled(string name)
        {
            return Task.FromResult(File.Exists($"/etc/systemd/system/multi-user.target.wants/{name}.service"));
        }

        public async Task<string> GetServiceExecutableAndArguments(string name)
        {
            var svcRegex = new Regex("^ExecStart=(.*)$", RegexOptions.Multiline);
            var execStart = svcRegex.Match(await File.ReadAllTextAsync($"/etc/systemd/system/{name}.service").ConfigureAwait(false)).Groups[1].Value;
            return execStart.Trim();
        }

        public async Task<bool> IsServiceRunning(string name, CancellationToken cancellationToken)
        {
            var checkProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/systemctl",
                ArgumentList =
                {
                    "is-active",
                    "--quiet",
                    $"{name}.service"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!;
            await checkProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return checkProcess.ExitCode == 0;
        }

        public async Task InstallService(
            string name,
            string displayName,
            string executableAndArguments,
            string? stdoutLogPath,
            string? stderrLogPath,
            bool manualStart)
        {
            await File.WriteAllTextAsync($"/etc/systemd/system/{name}.service", @$"
[Unit]
Description={displayName}
StartLimitIntervalSec=0

[Service]
ExecStart={executableAndArguments}
Restart=always
RestartSec=60

[Install]
{(manualStart ? "" : "WantedBy=multi-user.target")}
").ConfigureAwait(false);
            if (!manualStart)
            {
                File.CreateSymbolicLink($"/etc/systemd/system/multi-user.target.wants/{name}.service", $"/etc/systemd/system/{name}.service");
            }
            await Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/systemctl",
                ArgumentList =
                {
                    "daemon-reload"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync().ConfigureAwait(false);
        }

        public async Task StartService(string name, CancellationToken cancellationToken)
        {
            await Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/systemctl",
                ArgumentList =
                {
                    "start",
                    $"{name}.service"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task StopService(string name, CancellationToken cancellationToken)
        {
            await Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/systemctl",
                ArgumentList =
                {
                    "stop",
                    $"{name}.service"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task UninstallService(string name)
        {
            if (File.Exists($"/etc/systemd/system/multi-user.target.wants/{name}.service"))
            {
                File.Delete($"/etc/systemd/system/multi-user.target.wants/{name}.service");
            }
            await Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/systemctl",
                ArgumentList =
                {
                    "daemon-reload"
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync().ConfigureAwait(false);
        }

        public async Task StreamLogsUntilCancelledAsync(
            string name,
            Action<ServiceLogLevel, string> receiveLog,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(receiveLog);

            if (_processExecutor == null)
            {
                throw new PlatformNotSupportedException("Missing IProcessExecutor service at runtime; StreamLogsUntilCancelledAsync can't be used on this platform without that service.");
            }

            // Just use journalctl to monitor the service logs.
            try
            {
                await foreach (var message in _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/usr/bin/journalctl",
                        Arguments =
                        [
                            "-fu",
                            $"{name}.service"
                        ]
                    },
                    cancellationToken))
                {
                    switch (message)
                    {
                        case StandardOutputResponse stdout:
                            receiveLog(ServiceLogLevel.Information, stdout.Data.Trim());
                            break;
                        case StandardErrorResponse stderr:
                            receiveLog(ServiceLogLevel.Error, stderr.Data.Trim());
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

}
