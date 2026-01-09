namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultReboot : IReboot
    {
        private readonly ILogger<DefaultReboot> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;

        public DefaultReboot(
            ILogger<DefaultReboot> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
        }

        public async Task RebootMachine(CancellationToken cancellationToken)
        {
            // Reboot the machine.
            _logger.LogInformation("Rebooting machine...");
            if (OperatingSystem.IsWindows())
            {
                string? shutdown = null;
                try
                {
                    shutdown = await _pathResolver.ResolveBinaryPath("shutdown");
                }
                catch (FileNotFoundException)
                {
                }
                if (shutdown != null)
                {
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = shutdown,
                            Arguments = ["/g", "/t", "0", "/c", "RKM Provisioning", "/f", "/d", "p:4:1"]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                }
                else if (File.Exists(@"X:\windows\System32\WindowsPowerShell\v1.0\powershell.exe"))
                {
                    // For some reason, ResolveBinaryPath doesn't find powershell.exe on Windows PE,
                    // but it's always at the same path.
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = @"X:\windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                            Arguments = ["-Command", "Restart-Computer -Force"]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                }
                else
                {
                    // Try to search for powershell.exe.
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath("powershell"),
                            Arguments = ["-Command", "Restart-Computer -Force"]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = await _pathResolver.ResolveBinaryPath("shutdown"),
                        Arguments = ["-r", "now"]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
            }
            else if (OperatingSystem.IsLinux())
            {
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = await _pathResolver.ResolveBinaryPath("systemctl"),
                        Arguments = ["--message=\"RKM Provisioning\"", "reboot"]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            // Sleep indefinitely until the machine reboots.
            await Task.Delay(-1, cancellationToken);
        }
    }
}
