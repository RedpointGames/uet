namespace Redpoint.KubernetesManager.Components
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Windows;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.Windows.HostNetworkingService;
    using Redpoint.PackageManagement;

    /// <summary>
    /// The kube proxy component sets up and runs the kube-proxy process.
    /// </summary>
    internal class KubeProxyComponent : IComponent
    {
        private readonly ILogger<KubeProxyComponent> _logger;
        private readonly IResourceManager _resourceManager;
        private readonly IPathProvider _pathProvider;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IKubeConfigManager _kubeConfigManager;
        private readonly IPackageManager _packageManager;
        private readonly IHnsApi? _hnsService;

        public KubeProxyComponent(
            ILogger<KubeProxyComponent> logger,
            IResourceManager resourceManager,
            IPathProvider pathProvider,
            IProcessMonitorFactory processMonitorFactory,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IKubeConfigManager kubeConfigManager,
            IPackageManager packageManager,
            IHnsApi? hnsService = null)
        {
            _logger = logger;
            _resourceManager = resourceManager;
            _pathProvider = pathProvider;
            _processMonitorFactory = processMonitorFactory;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _kubeConfigManager = kubeConfigManager;
            _packageManager = packageManager;
            _hnsService = hnsService;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeConfigsReady);
            var nodeNameContext = await context.WaitForFlagAsync<NodeNameContextData>(WellKnownFlags.NodeComponentsReadyToStart);
            var nodeName = nodeNameContext.NodeName;

            if (OperatingSystem.IsLinux())
            {
                _logger.LogInformation("Ensuring conntrack is installed...");
                try
                {
                    await _packageManager.InstallOrUpgradePackageToLatestAsync("conntrack", cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Unable to install conntrack package: {ex}");
                }
            }

            _logger.LogInformation("Setting up kube-proxy configuration...");
            if (OperatingSystem.IsLinux())
            {
                await _resourceManager.ExtractResource(
                    "kube-proxy-config-linux.yaml",
                    Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kube-proxy-config.yaml"),
                    new Dictionary<string, string>
                    {
                        { "__KUBECONFIG__", _kubeConfigManager.GetKubeconfigPath("components", "component-kube-proxy") },
                        { "__CLUSTER_CIDR__", _clusterNetworkingConfiguration.ClusterCIDR }
                    });
            }
            else if (OperatingSystem.IsWindows())
            {
                // On Windows, we must also wait for the Calico on Windows component to be ready
                // and provide us the source VIP.
                var calicoWindowsContext = await context.WaitForFlagAsync<CalicoWindowsContextData>(WellKnownFlags.CalicoWindowsReady);

                await _resourceManager.ExtractResource(
                    "kube-proxy-config-windows.yaml",
                    Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kube-proxy-config.yaml"),
                    new Dictionary<string, string>
                    {
                        { "__NETWORK_NAME__", _clusterNetworkingConfiguration.HnsNetworkName },
                        { "__KUBECONFIG__", _kubeConfigManager.GetKubeconfigPath("components", "component-kube-proxy").Replace("\\", "\\\\", StringComparison.Ordinal) },
                        { "__HOSTNAME__", nodeName },
                        { "__SOURCE_VIP__", calicoWindowsContext.SourceVIP }
                    });
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            _logger.LogInformation("Starting kube-proxy and keeping it running...");
            var kubeProxyMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                filename: Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kubernetes", "node", "bin", "kube-proxy"),
                arguments: new[]
                {
                    $"--config={Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kube-proxy-config.yaml")}",
                    $"--v=4",
                },
                beforeStart: (cancellationToken) =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        // Clear out any HNS policy lists before kube-proxy starts each time, otherwise it will get confused.
                        foreach (var policyList in _hnsService!.GetHnsPolicyLists())
                        {
                            _logger.LogInformation($"Removing stale policy list before kube-proxy starts: {policyList.Id}");
                            _hnsService.DeleteHnsPolicyList(policyList.Id);
                        }
                    }
                    return Task.CompletedTask;
                }));
            await kubeProxyMonitor.RunAsync(cancellationToken);
        }
    }
}
