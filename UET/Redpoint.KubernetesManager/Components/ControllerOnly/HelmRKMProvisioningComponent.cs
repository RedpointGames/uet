namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Helm;
    using Redpoint.KubernetesManager.Services.Windows;
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
    internal class HelmRKMProvisioningComponent : IComponent
    {
        private readonly IPathProvider _pathProvider;
        private readonly ILogger<HelmRKMProvisioningComponent> _logger;
        private readonly IRkmVersionProvider _rkmVersionProvider;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IHelmDeployment _helmDeployment;
        private readonly IWslTranslation _wslTranslation;

        public HelmRKMProvisioningComponent(
            IPathProvider pathProvider,
            ILogger<HelmRKMProvisioningComponent> logger,
            IRkmVersionProvider rkmVersionProvider,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            ILocalEthernetInfo localEthernetInfo,
            IHelmDeployment helmDeployment,
            IWslTranslation wslTranslation)
        {
            _pathProvider = pathProvider;
            _logger = logger;
            _rkmVersionProvider = rkmVersionProvider;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _localEthernetInfo = localEthernetInfo;
            _helmDeployment = helmDeployment;
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
            // Deploy RKM components, including Calico, via Helm.
            var exitCode = await _helmDeployment.DeployChart(
                context,
                "rkm-components",
                $"oci://ghcr.io/redpointgames/uet/rkm-components:{_rkmVersionProvider.Version}",
                $"""
                versions:
                  rkm: "{_rkmVersionProvider.Version}"

                calico:
                  root: "/opt/rkm/{_pathProvider.RKMInstallationId}/calico"
                  cni:
                    mtu: 1500
                    pluginsRoot: "/opt/rkm/{_pathProvider.RKMInstallationId}/cni-plugins"

                containerd:
                  root: "/opt/rkm/{_pathProvider.RKMInstallationId}/containerd-state"

                cluster:
                  cidr: "{_clusterNetworkingConfiguration.ClusterCIDR}"
                  serviceCidr: "{_clusterNetworkingConfiguration.ServiceCIDR}"
                  dnsServiceIp: "{_clusterNetworkingConfiguration.ClusterDNSServiceIP}"
                  dnsDomain: "{_clusterNetworkingConfiguration.ClusterDNSDomain}"
                  controllerIp: "{await _wslTranslation.GetTranslatedIPAddress(cancellationToken)}"

                host:
                  subnet:
                    cidr: "{_localEthernetInfo.HostSubnetCIDR!}"
                """,
                waitForResourceStabilisation: false, // Only wait for hooks, in case some nodes are currently offline.
                cancellationToken);
            if (exitCode != 0)
            {
                _logger.LogCritical("rkm is exiting because it could not install or upgrade the Helm charts into the cluster, and they are required for the cluster to work.");
                context.StopOnCriticalError();
                return;
            }

            // Flag once the RKM components have been provisioned.
            context.SetFlag(WellKnownFlags.HelmChartProvisioned);
        }
    }
}
