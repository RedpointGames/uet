namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// This component installs/upgrades the 'RKM Components' Helm chart in the cluster.
    /// 
    /// This component only runs on the controller.
    /// </summary>
    internal class HelmChartUpgradeProvisioningComponent : IComponent
    {
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IPathProvider _pathProvider;
        private readonly IKubeConfigManager _kubeConfigManager;
        private readonly ILogger<HelmChartUpgradeProvisioningComponent> _logger;
        private readonly IRkmVersionProvider _rkmVersionProvider;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly ILocalEthernetInfo _localEthernetInfo;

        public HelmChartUpgradeProvisioningComponent(
            IProcessMonitorFactory processMonitorFactory,
            IPathProvider pathProvider,
            IKubeConfigManager kubeConfigManager,
            ILogger<HelmChartUpgradeProvisioningComponent> logger,
            IRkmVersionProvider rkmVersionProvider,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            ILocalEthernetInfo localEthernetInfo)
        {
            _processMonitorFactory = processMonitorFactory;
            _pathProvider = pathProvider;
            _kubeConfigManager = kubeConfigManager;
            _logger = logger;
            _rkmVersionProvider = rkmVersionProvider;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _localEthernetInfo = localEthernetInfo;
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

            // Provision via Helm.
            var helm = _processMonitorFactory.CreateTerminatingProcess(new ProcessSpecification(
                filename: Path.Combine(_pathProvider.RKMRoot, "helm-bin", "helm"),
                arguments: [
                    $"--kubeconfig={_kubeConfigManager.GetKubeconfigPath("users", "user-admin")}",
                    "upgrade",
                    "--install",
                    "rkm-components",
                    "-n",
                    "kube-system",
                    $"oci://ghcr.io/redpointgames/uet/rkm-components:{_rkmVersionProvider.Version}",
                    "--wait",
                    "--set", $"calico.root=/opt/rkm/{_pathProvider.RKMInstallationId}/calico",
                    "--set", $"calico.cni.pluginsRoot=/opt/rkm/{_pathProvider.RKMInstallationId}/cni-plugins",
                    "--set", $"containerd.root=/opt/rkm/{_pathProvider.RKMInstallationId}/containerd-state",
                    "--set", $"cluster.cidr={_clusterNetworkingConfiguration.ClusterCIDR}",
                    "--set", $"host.subnet.cidr={_localEthernetInfo.HostSubnetCIDR!}",
                ]));
            if ((await helm.RunAsync(cancellationToken)) != 0)
            {
                _logger.LogCritical("rkm is exiting because it could not install or upgrade the Helm charts into the cluster, and they are required for the cluster to work.");
                context.StopOnCriticalError();
                return;
            }

            // Once the Helm chart is deployed, Calico has been provisioned.
            context.SetFlag(WellKnownFlags.CalicoProvisioned);
        }
    }
}
