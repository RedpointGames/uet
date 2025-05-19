namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The kubelet component sets up and runs the kubelet process.
    /// </summary>
    internal class KubeletComponent : IComponent
    {
        private readonly ILogger<KubeletComponent> _logger;
        private readonly IResourceManager _resourceManager;
        private readonly IPathProvider _pathProvider;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly ICertificateManager _certificateManager;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IKubeConfigManager _kubeConfigManager;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IRkmGlobalRootProvider _rkmGlobalRootProvider;

        public KubeletComponent(
            ILogger<KubeletComponent> logger,
            IResourceManager resourceManager,
            IPathProvider pathProvider,
            IProcessMonitorFactory processMonitorFactory,
            ICertificateManager certificateManager,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IKubeConfigManager kubeConfigManager,
            ILocalEthernetInfo localEthernetInfo,
            IRkmGlobalRootProvider rkmGlobalRootProvider)
        {
            _logger = logger;
            _resourceManager = resourceManager;
            _pathProvider = pathProvider;
            _processMonitorFactory = processMonitorFactory;
            _certificateManager = certificateManager;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _kubeConfigManager = kubeConfigManager;
            _localEthernetInfo = localEthernetInfo;
            _rkmGlobalRootProvider = rkmGlobalRootProvider;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
            await context.WaitForFlagAsync(WellKnownFlags.CertificatesReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeConfigsReady);
            var nodeNameContext = await context.WaitForFlagAsync<NodeNameContextData>(WellKnownFlags.NodeComponentsReadyToStart);
            var nodeName = nodeNameContext.NodeName;

            _logger.LogInformation("Setting up WSL configuration...");
            if (OperatingSystem.IsLinux())
            {
                await _resourceManager.ExtractResource(
                    "kubelet-config-linux.yaml",
                     Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kubelet-config.yaml"),
                    new Dictionary<string, string>
                    {
                        { "__CA_CERT_FILE__", _certificateManager.GetCertificatePemPath("ca", "ca") },
                        { "__NODE_CERT_FILE__", _certificateManager.GetCertificatePemPath("nodes", $"node-{nodeName}") },
                        { "__NODE_KEY_FILE__", _certificateManager.GetCertificateKeyPath("nodes", $"node-{nodeName}") },
                        { "__CLUSTER_DNS__", _clusterNetworkingConfiguration.ClusterDNSServiceIP },
                        { "__CLUSTER_DOMAIN__", _clusterNetworkingConfiguration.ClusterDNSDomain },
                        // If systemd is running, prevent bugs by using /run/systemd/resolve/resolv.conf directly.
                        // Otherwise use /etc/resolv.conf for systems that are not running resolved.
                        { "__RESOLV_CONF__", Process.GetProcessesByName("systemd-resolved").Length > 0 ? "/run/systemd/resolve/resolv.conf" : "/etc/resolv.conf" },
                    });
            }
            else if (OperatingSystem.IsWindows())
            {
                await _resourceManager.ExtractResource(
                    "kubelet-config-windows.yaml",
                     Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kubelet-config.yaml"),
                    new Dictionary<string, string>
                    {
                        { "__CA_CERT_FILE__", _certificateManager.GetCertificatePemPath("ca", "ca").Replace("\\", "\\\\", StringComparison.Ordinal) },
                        { "__NODE_CERT_FILE__", _certificateManager.GetCertificatePemPath("nodes", $"node-{nodeName}").Replace("\\", "\\\\", StringComparison.Ordinal) },
                        { "__NODE_KEY_FILE__", _certificateManager.GetCertificateKeyPath("nodes", $"node-{nodeName}").Replace("\\", "\\\\", StringComparison.Ordinal) },
                        // When Windows is running as a controller, it runs it's own CoreDNS instance on the Windows side for containers
                        // (as containers can't seem to send UDP traffic over the configured Hyper-V WSL+overlay switch).
                        { "__CLUSTER_DNS__", context.Role == RoleType.Controller ? _localEthernetInfo.IPAddress.ToString() : _clusterNetworkingConfiguration.ClusterDNSServiceIP },
                        { "__CLUSTER_DOMAIN__", _clusterNetworkingConfiguration.ClusterDNSDomain },
                    });
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            string endpoint;
            if (OperatingSystem.IsWindows())
            {
                endpoint = "npipe://./pipe/containerd-containerd";
            }
            else
            {
                endpoint = $"unix://{Path.Combine(_pathProvider.RKMRoot, "containerd-state", "containerd.sock")}";
            }

            _logger.LogInformation("Starting kubelet and keeping it running...");
            var kubeletMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                filename: Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kubernetes", "node", "bin", "kubelet"),
                arguments: new[]
                {
                    $"--config={Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kubelet-config.yaml")}",
                    $"--container-runtime=remote",
                    $"--container-runtime-endpoint={endpoint}",
                    $"--kubeconfig={_kubeConfigManager.GetKubeconfigPath("nodes", $"node-{nodeName}")}",
                    $"--root-dir={Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "state")}",
                    $"--cert-dir={Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "state", "pki")}",
                    $"--v=2",
                    "--node-labels",
                    $"rkm.redpoint.games/auto-upgrade-enabled={(File.Exists(Path.Combine(_rkmGlobalRootProvider.RkmGlobalRoot, "service-auto-upgrade")) ? "true" : "false")}",
                },
                afterStart: async (cancellationToken) =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        // Each time kubelet starts up (including restarts), we need to ask
                        // calico-node to reconfigure the machine's network. This ensures that
                        // calico refreshes the node annotations if kubelet recreates the node
                        // resource.
                        await context.RaiseSignalAsync(WellKnownSignals.KubeletProcessStartedOnWindows, null, cancellationToken);
                    }
                }));
            try
            {
                await kubeletMonitor.RunAsync(cancellationToken);
            }
            finally
            {
                context.SetFlag(WellKnownFlags.KubeletStopped);
            }
        }
    }
}
