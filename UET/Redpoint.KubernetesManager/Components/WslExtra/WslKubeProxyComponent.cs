namespace Redpoint.KubernetesManager.Components.WslExtra
{
    using Redpoint.KubernetesManager.Implementations;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;

    /// <summary>
    /// The WSL kube proxy component sets up and runs a second kube-proxy instance
    /// inside WSL, specifically on Windows controllers where we need Linux
    /// pods.
    /// </summary>
    internal class WslKubeProxyComponent : IComponent
    {
        private readonly ILogger<WslKubeProxyComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IResourceManager _resourceManager;
        private readonly IKubeConfigManager _kubeConfigManager;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IWslTranslation _wslTranslation;

        public WslKubeProxyComponent(
            ILogger<WslKubeProxyComponent> logger,
            IPathProvider pathProvider,
            IResourceManager resourceManager,
            IKubeConfigManager kubeConfigManager,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IProcessMonitorFactory processMonitorFactory,
            IWslTranslation wslTranslation)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _resourceManager = resourceManager;
            _kubeConfigManager = kubeConfigManager;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _processMonitorFactory = processMonitorFactory;
            _wslTranslation = wslTranslation;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller && OperatingSystem.IsWindows())
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeConfigsReady);
            var nodeNameContext = await context.WaitForFlagAsync<NodeNameContextData>(WellKnownFlags.NodeComponentsReadyToStart);
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            _logger.LogInformation("Setting up WSL kube-proxy configuration...");
            await _resourceManager.ExtractResource(
                "kube-proxy-config-linux.yaml",
                Path.Combine(_pathProvider.RKMRoot, "wsl", "kubernetes-node", "kube-proxy-config.yaml"),
                new Dictionary<string, string>
                {
                    { "__KUBECONFIG__", _wslTranslation.TranslatePath(_kubeConfigManager.GetKubeconfigPath("components", "component-kube-proxy")) },
                    { "__CLUSTER_CIDR__", _clusterNetworkingConfiguration.ClusterCIDR }
                });

            _logger.LogInformation("Starting WSL kube-proxy and keeping it running...");
            var kubeProxyMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                filename: _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "wsl", "kubernetes-node", "kubernetes", "node", "bin", "kube-proxy")),
                arguments: new[]
                {
                    $"--config={_wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "wsl", "kubernetes-node", "kube-proxy-config.yaml"))}",
                    $"--v=4",
                },
                wsl: true));
            await kubeProxyMonitor.RunAsync(cancellationToken);
        }
    }
}
