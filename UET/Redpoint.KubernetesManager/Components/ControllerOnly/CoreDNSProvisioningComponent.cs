namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The CoreDNS provisioning component installs or upgrades CoreDNS
    /// in the cluster. It waits for the API server to be available
    /// so that it can then run kubectl apply.
    /// 
    /// This component only runs on the controller.
    /// </summary>
    internal class CoreDNSProvisioningComponent : IComponent
    {
        private readonly ILogger<CoreDNSProvisioningComponent> _logger;
        private readonly IResourceManager _resourceManager;
        private readonly IPathProvider _pathProvider;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IKubeConfigManager _kubeConfigManager;

        public CoreDNSProvisioningComponent(
            ILogger<CoreDNSProvisioningComponent> logger,
            IResourceManager resourceManager,
            IPathProvider pathProvider,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            ILocalEthernetInfo localEthernetInfo,
            IProcessMonitorFactory processMonitorFactory,
            IKubeConfigManager kubeConfigManager)
        {
            _logger = logger;
            _resourceManager = resourceManager;
            _pathProvider = pathProvider;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _localEthernetInfo = localEthernetInfo;
            _processMonitorFactory = processMonitorFactory;
            _kubeConfigManager = kubeConfigManager;
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

            // Provision coredns via kubectl (applying a YAML file via the API is messy).
            await _resourceManager.ExtractResource(
                "coredns.yaml",
                Path.Combine(_pathProvider.RKMRoot, "coredns", "coredns.yaml"),
                new Dictionary<string, string>
                {
                    { "__CLUSTER_DOMAIN__", _clusterNetworkingConfiguration.ClusterDNSDomain },
                    { "__CLUSTER_DNS__", _clusterNetworkingConfiguration.ClusterDNSServiceIP },
                });
            var kubectl = _processMonitorFactory.CreateTerminatingProcess(new ProcessSpecification(
                // Note we use kubectl from the kubernetes-node install because kubernetes-server's version will be a Linux binary on Windows.
                filename: Path.Combine(_pathProvider.RKMRoot, "kubernetes-node", "kubernetes", "node", "bin", "kubectl"),
                arguments: new[]
                {
                    $"--kubeconfig={_kubeConfigManager.GetKubeconfigPath("users", "user-admin")}",
                    "apply",
                    "-f",
                    Path.Combine(_pathProvider.RKMRoot, "coredns", "coredns.yaml")
                }));
            if ((await kubectl.RunAsync(cancellationToken)) != 0)
            {
                _logger.LogCritical("rkm is exiting because it could not install CoreDNS into the cluster, and CoreDNS is required for networking to work.");
                context.StopOnCriticalError();
                return;
            }

            context.SetFlag(WellKnownFlags.CoreDNSProvisioned);
        }
    }
}
