namespace Redpoint.KubernetesManager.Services
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;
    using System;
    using System.Diagnostics;
    using System.Text;

    internal class DefaultProcessKiller : IProcessKiller
    {
        private readonly ILogger<DefaultProcessKiller> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IWslDistro _wslDistro;
        private readonly IWindowsHcsService _hcsService;

        public DefaultProcessKiller(
            ILogger<DefaultProcessKiller> logger,
            IPathProvider pathProvider,
            IWslDistro wslDistro,
            IWindowsHcsService hcsService)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _wslDistro = wslDistro;
            _hcsService = hcsService;
        }

        public async Task EnsureProcessesAreNotRunning(CancellationToken cancellationToken)
        {
            await EnsureProcessesAreNotRunning([
                "containerd",
                "containerd-shim-runhcs-v1",
                "flanneld",
                "kubelet",
                "kube-proxy",
                "etcd",
                "kube-apiserver",
                "kube-controller-manager",
                "kube-scheduler",
            ], cancellationToken);
        }

        private async Task EnsureProcessesAreNotRunning(string[] processNames, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Ensuring existing Kubernetes processes are not running...");
            foreach (var processName in processNames)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var processes = Process.GetProcessesByName(processName);
                    foreach (var process in processes)
                    {
                        try
                        {
                            _logger.LogInformation($"Killing process {process.ProcessName} ({process.Id})...");
                            process.Kill();
                        }
                        catch
                        {
                            _logger.LogWarning($"Unable to kill process {process.ProcessName} ({process.Id})!");
                        }
                    }
                    if (processes.Length == 0)
                    {
                        break;
                    }
                    else
                    {
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(_wslDistro.WslPath))
                {
                    _logger.LogInformation("Ensuring existing RKM processes inside WSL are not running...");

                    var runningDistributions = await _wslDistro.CaptureWslInvocation(
                        new[] { "-l", "--running" },
                        Encoding.Unicode,
                        cancellationToken);
                    if (runningDistributions.Contains($"RKM-Kubernetes-{_pathProvider.RKMInstallationId}", StringComparison.Ordinal))
                    {
                        // Just terminate the whole WSL environment, since we now control the WSL distribution.
                        await _wslDistro.RunWslInvocation(new[] { "-t", $"RKM-Kubernetes-{_pathProvider.RKMInstallationId}" }, string.Empty, Encoding.UTF8, cancellationToken);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                foreach (var computeSystem in _hcsService.GetHcsComputeSystems())
                {
                    if (computeSystem.SystemType == "Container")
                    {
                        _logger.LogInformation($"Killing HCS compute system {computeSystem.Id}...");
                        _hcsService.TerminateHcsSystem(computeSystem.Id);
                    }
                }
            }
        }
    }
}

