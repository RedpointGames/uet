﻿namespace Redpoint.ServiceControl
{
    using System.Diagnostics;
    using System.Runtime.Versioning;
    using System.Security.Principal;
    using System.Text.RegularExpressions;
    using System.Threading;

    [SupportedOSPlatform("windows")]
    public sealed class WindowsServiceControl : IServiceControl
    {
        public bool HasPermissionToInstall
        {
            get
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
        }

        public bool HasPermissionToStart => true;

        public async Task<bool> IsServiceInstalled(string name)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "sc.exe"),
                ArgumentList =
                    {
                        "query",
                        name
                    },
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            })!;
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0;
        }

        public async Task<string> GetServiceExecutableAndArguments(string name)
        {
            var binPathRegex = new Regex("^\\s+BINARY_PATH_NAME\\s+:\\s(.*)$", RegexOptions.Multiline);
            var binPathDetectProcess = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "sc.exe"),
                ArgumentList =
                {
                    "qc",
                    name
                },
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            })!;
            var scOutput = await binPathDetectProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var binPath = binPathRegex.Match(scOutput).Groups[1].Value;
            await binPathDetectProcess.WaitForExitAsync().ConfigureAwait(false);
            return binPath.Trim();
        }

        public async Task<bool> IsServiceRunning(string name)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "sc.exe"),
                ArgumentList =
                    {
                        "query",
                        name
                    },
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            })!;
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            return output.Contains("RUNNING", StringComparison.Ordinal);
        }

        public async Task InstallService(
            string name,
            string description,
            string executableAndArguments,
            string? stdoutLogPath,
            string? stderrLogPath,
            bool manualStart)
        {
            await Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "sc.exe"),
                ArgumentList =
                    {
                        "create",
                        name,
                        $"binpath={executableAndArguments}",
                        "obj=LocalSystem",
                        $"start={(manualStart ? "manual" : "auto")}"
                    },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync().ConfigureAwait(false);
            await Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "sc.exe"),
                ArgumentList =
                    {
                        "failure",
                        name,
                        "reset=1",
                        "actions=restart/60000/restart/60000/restart/60000"
                    },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync().ConfigureAwait(false);
            await Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "sc.exe"),
                ArgumentList =
                    {
                        "description",
                        name,
                        description,
                    },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync().ConfigureAwait(false);
        }

        public async Task StartService(string name)
        {
            await Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "sc.exe"),
                ArgumentList =
                {
                    "start",
                    name
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync().ConfigureAwait(false);
        }

        public async Task StopService(string name)
        {
            await Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "sc.exe"),
                ArgumentList =
                {
                    "stop",
                    name
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync().ConfigureAwait(false);
        }

        public async Task UninstallService(string name)
        {
            await Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "sc.exe"),
                ArgumentList =
                {
                    "delete",
                    name
                },
                CreateNoWindow = true,
                UseShellExecute = false,
            })!.WaitForExitAsync().ConfigureAwait(false);

            while (await IsServiceInstalled(name).ConfigureAwait(false))
            {
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        public async Task StreamLogsUntilCancelledAsync(
            string name,
            Action<ServiceLogLevel, string> receiveLog,
            CancellationToken cancellationToken)
        {
            // Connect to the Event Log, monitor for RKM log entries, and emit.
            var eventLog = new EventLog("Application");
            cancellationToken.Register(() =>
            {
                eventLog.EnableRaisingEvents = false;
            });
            eventLog.EnableRaisingEvents = true;
            eventLog.EntryWritten += (sender, args) =>
            {
                if (args.Entry.Source.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    var lines = args.Entry.Message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
                    var category = lines[0].Substring("Category:".Length).Trim();
                    var message = string.Join("\n", lines.Skip(3)).Trim();

                    switch (args.Entry.EntryType)
                    {
                        case EventLogEntryType.Information:
                        case EventLogEntryType.SuccessAudit:
                            receiveLog(ServiceLogLevel.Information, message);
                            break;
                        case EventLogEntryType.Warning:
                            receiveLog(ServiceLogLevel.Warning, message);
                            break;
                        case EventLogEntryType.Error:
                        case EventLogEntryType.FailureAudit:
                            receiveLog(ServiceLogLevel.Error, message);
                            break;
                    }
                }
            };
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(-1, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

}