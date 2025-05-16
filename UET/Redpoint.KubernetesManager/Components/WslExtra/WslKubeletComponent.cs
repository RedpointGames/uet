namespace Redpoint.KubernetesManager.Components.WslExtra
{
    using Microsoft.Extensions.Hosting;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System;
    using System.Diagnostics;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;

    /// <summary>
    /// The WSL kubelet component sets up and runs a second kubelet instance
    /// inside WSL, specifically on Windows controllers where we need Linux
    /// pods.
    /// </summary>
    internal class WslKubeletComponent : IComponent
    {
        private readonly ILogger<WslKubeletComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IResourceManager _resourceManager;
        private readonly ICertificateManager _certificateManager;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IWslTranslation _wslTranslation;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IKubeConfigManager _kubeConfigManager;
        private readonly IWslDistro _wslDistro;

        public WslKubeletComponent(
            ILogger<WslKubeletComponent> logger,
            IPathProvider pathProvider,
            IResourceManager resourceManager,
            ICertificateManager certificateManager,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IWslTranslation wslTranslation,
            IProcessMonitorFactory processMonitorFactory,
            IKubeConfigManager kubeConfigManager,
            IWslDistro wslDistro)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _resourceManager = resourceManager;
            _certificateManager = certificateManager;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _wslTranslation = wslTranslation;
            _processMonitorFactory = processMonitorFactory;
            _kubeConfigManager = kubeConfigManager;
            _wslDistro = wslDistro;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller && OperatingSystem.IsWindows())
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        [SupportedOSPlatform("windows")]
        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
            await context.WaitForFlagAsync(WellKnownFlags.CertificatesReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeConfigsReady);
            var nodeNameContext = await context.WaitForFlagAsync<NodeNameContextData>(WellKnownFlags.NodeComponentsReadyToStart);
            var nodeName = nodeNameContext.NodeName;
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            // Our WSL kubelet also needs to have the server root symlinked like Linux kubelets do.
            var wslSymlinkPath = Path.Combine(_pathProvider.RKMRoot, "wsl", "kubelet-symlink");
            await File.WriteAllTextAsync(wslSymlinkPath, $@"
#!/bin/bash
if [ ! -e /opt/rkm/{_pathProvider.RKMInstallationId} ]; then
    mkdir -p /opt/rkm/{_pathProvider.RKMInstallationId}
    # containerd has to symlink to the Linux folder where we store state
    ln -s ""/run/{_pathProvider.RKMInstallationId}-containerd"" /opt/rkm/{_pathProvider.RKMInstallationId}/containerd
    # calico just has to be a folder that calico can store stuff per node
    mkdir -p /opt/rkm/{_pathProvider.RKMInstallationId}/calico
    # cni-plugins has to symlink to the Windows path where they're stored
    ln -s ""{_wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "wsl"))}/cni-plugins"" /opt/rkm/{_pathProvider.RKMInstallationId}/cni-plugins
fi
".Replace("\r\n", "\n", StringComparison.Ordinal), cancellationToken);
            var distroName = await _wslDistro.GetWslDistroName(cancellationToken);
            var symlinkExitCode = await _wslDistro.RunWslInvocation(new[] { "-d", distroName, "-u", "root", "-e", "/bin/bash", _wslTranslation.TranslatePath(wslSymlinkPath) }, string.Empty, Encoding.UTF8, CancellationToken.None);
            if (symlinkExitCode != 0)
            {
                _logger.LogCritical($"Failed to symlink installation directory inside WSL (got exit code {symlinkExitCode}). See above for details.");
                context.StopOnCriticalError();
                return;
            }

            // We need to turn off swap in WSL before starting the kubelet.
            var swapsOffExitCode = await _wslDistro.RunWslInvocation(new[] { "-d", distroName, "-u", "root", "-e", "/usr/sbin/swapoff", "-a" }, string.Empty, Encoding.UTF8, CancellationToken.None);
            if (swapsOffExitCode != 0)
            {
                _logger.LogCritical($"Failed to turn off swap inside WSL (got exit code {swapsOffExitCode}). See above for details.");
                context.StopOnCriticalError();
                return;
            }

            _logger.LogInformation("Setting up WSL kubelet configuration...");
            await _resourceManager.ExtractResource(
                "kubelet-config-linux.yaml",
                Path.Combine(_pathProvider.RKMRoot, "wsl", "kubernetes-node", "kubelet-config.yaml"),
                new Dictionary<string, string>
                {
                    { "__CA_CERT_FILE__", _wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("ca", "ca")) },
                    { "__NODE_CERT_FILE__", _wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("nodes", $"node-{nodeName}-wsl")) },
                    { "__NODE_KEY_FILE__", _wslTranslation.TranslatePath(_certificateManager.GetCertificateKeyPath("nodes", $"node-{nodeName}-wsl")) },
                    { "__CLUSTER_DNS__", _clusterNetworkingConfiguration.ClusterDNSServiceIP },
                    { "__CLUSTER_DOMAIN__", _clusterNetworkingConfiguration.ClusterDNSDomain },
                    // We know WSL will be running systemd-resolved, so no need to check process existence here.
                    { "__RESOLV_CONF__", "/run/systemd/resolve/resolv.conf" },
                });

            var endpoint = $"unix:///run/{_pathProvider.RKMInstallationId}-containerd/containerd.sock";

            _logger.LogInformation("Starting WSL kubelet and keeping it running...");
            var kubeletMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                filename: _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "wsl", "kubernetes-node", "kubernetes", "node", "bin", "kubelet")),
                arguments: new[]
                {
                    $"--config={_wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "wsl", "kubernetes-node", "kubelet-config.yaml"))}",
                    $"--container-runtime=remote",
                    $"--container-runtime-endpoint={endpoint}",
                    $"--kubeconfig={_wslTranslation.TranslatePath(_kubeConfigManager.GetKubeconfigPath("nodes", $"node-{nodeName}-wsl"))}",
                    $"--root-dir=/run/{_pathProvider.RKMInstallationId}-kubernetes-state",
                    $"--cert-dir=/run/{_pathProvider.RKMInstallationId}-kubernetes-state/pki",
                    $"--v=2"
                },
                wsl: true));
            await kubeletMonitor.RunAsync(cancellationToken);
        }
    }
}
