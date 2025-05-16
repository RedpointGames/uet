namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;

    /// <summary>
    /// The Calico provisioning component installs or upgrades Calico
    /// in the cluster. It waits for the API server to be available
    /// so that it can then run kubectl apply.
    /// 
    /// This component only runs on the controller.
    /// </summary>
    internal class CalicoProvisioningComponent : IComponent
    {
        private readonly ILogger<CalicoProvisioningComponent> _logger;
        private readonly IResourceManager _resourceManager;
        private readonly IPathProvider _pathProvider;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IKubeConfigManager _kubeConfigManager;
        private readonly IWslTranslation _wslTranslation;

        public CalicoProvisioningComponent(
            ILogger<CalicoProvisioningComponent> logger,
            IResourceManager resourceManager,
            IPathProvider pathProvider,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            ILocalEthernetInfo localEthernetInfo,
            IProcessMonitorFactory processMonitorFactory,
            IKubeConfigManager kubeConfigManager,
            IWslTranslation wslTranslation)
        {
            _logger = logger;
            _resourceManager = resourceManager;
            _pathProvider = pathProvider;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _localEthernetInfo = localEthernetInfo;
            _processMonitorFactory = processMonitorFactory;
            _kubeConfigManager = kubeConfigManager;
            _wslTranslation = wslTranslation;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.KubeConfigsReady);

            // Wait for the Kubernetes API server to be available.
            var kubernetesContext = await context.WaitForFlagAsync<KubernetesClientContextData>(WellKnownFlags.KubeApiServerReady);
            var kubernetes = kubernetesContext.Kubernetes;

            // Provision calico via kubectl (applying a YAML file via the API is messy).
            await _resourceManager.ExtractResource(
                "calico.yaml",
                Path.Combine(_pathProvider.RKMRoot, "calico", "calico.yaml"),
                new Dictionary<string, string>
                {
                    // These paths are forced to be the Linux paths, because this code can run for a Windows controller.
                    // We also don't use TranslatePath since that will give us a path into the Windows filesystem from WSL
                    // and really what we want are paths as if this was installed on a native Linux node.
                    { "__CALICO_ROOT__", $"/opt/rkm/{_pathProvider.RKMInstallationId}/calico" },
                    { "__CONTAINERD_ROOT__", $"/opt/rkm/{_pathProvider.RKMInstallationId}/containerd-state" },
                    { "__CNI_PLUGINS_ROOT__", $"/opt/rkm/{_pathProvider.RKMInstallationId}/cni-plugins" },
                    { "__CLUSTER_CIDR__", _clusterNetworkingConfiguration.ClusterCIDR },
                    { "__HOST_SUBNET_CIDR__", _localEthernetInfo.HostSubnetCIDR! },
                });
            var kubectl = _processMonitorFactory.CreateTerminatingProcess(new ProcessSpecification(
                // Note we use kubectl from the kubernetes-node install because kubernetes-server's version will be a Linux binary on Windows.
                filename: Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kubernetes", "node", "bin", "kubectl"),
                arguments: new[]
                {
                    $"--kubeconfig={_kubeConfigManager.GetKubeconfigPath("users", "user-admin")}",
                    "apply",
                    "-f",
                    Path.Combine(_pathProvider.RKMRoot, "calico", "calico.yaml")
                }));
            if ((await kubectl.RunAsync(cancellationToken)) != 0)
            {
                _logger.LogCritical("rkm is exiting because it could not install Calico into the cluster, and Calico is required for networking to work.");
                context.StopOnCriticalError();
                return;
            }

            // Try to apply strictaffinity to IPAM in Calico, and repeatedly try until we succeed. That's because we
            // don't quite know when Calico will be available for configuration.
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Attempting to turn on strict affinity for Calico, which is required for Windows compatibility...");
                var calicoctl = _processMonitorFactory.CreateTerminatingProcess(new ProcessSpecification(
                    // @note: This points to the Linux script wrapper which we generate even on Windows because this command
                    // has to be run inside WSL and there's no way to set the KUBECONFIG variable at the WSL layer (so instead
                    // we let the wrapper script set the environment variable).
                    filename: _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "calicoctl")),
                    arguments: new[]
                    {
                        "ipam",
                        "configure",
                        "--strictaffinity=true"
                    },
                    wsl: true));
                if ((await calicoctl.RunAsync(cancellationToken)) != 0)
                {
                    _logger.LogWarning("Failed to apply strict affinity on Calico (it's probably not ready yet). Retrying in 1 seconds...");
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }
                break;
            }
            _logger.LogInformation("Successfully turned on strict affinity for Calico.");

            context.SetFlag(WellKnownFlags.CalicoProvisioned);
        }
    }
}
