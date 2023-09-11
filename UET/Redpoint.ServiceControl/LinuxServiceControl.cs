namespace Redpoint.ServiceControl
{
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    [SupportedOSPlatform("linux")]
    internal sealed class LinuxServiceControl : IServiceControl
    {
        [DllImport("libc")]
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

        public async Task<bool> IsServiceRunning(string name)
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
            await checkProcess.WaitForExitAsync().ConfigureAwait(false);
            return checkProcess.ExitCode == 0;
        }

        public async Task InstallService(string name, string description, string executableAndArguments, string? stdoutLogPath, string? stderrLogPath)
        {
            await File.WriteAllTextAsync($"/etc/systemd/system/{name}.service", @$"
[Unit]
Description={description}

[Service]
ExecStart={executableAndArguments}
Restart=always

[Install]
WantedBy=multi-user.target
").ConfigureAwait(false);
            File.CreateSymbolicLink($"/etc/systemd/system/multi-user.target.wants/{name}.service", $"/etc/systemd/system/{name}.service");
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

        public async Task StartService(string name)
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
            })!.WaitForExitAsync().ConfigureAwait(false);
        }

        public async Task StopService(string name)
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
            })!.WaitForExitAsync().ConfigureAwait(false);
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
    }

}
