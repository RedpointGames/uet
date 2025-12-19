namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Helm;
    using Redpoint.KubernetesManager.Services.Wsl;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using Redpoint.KubernetesManager.Versions;
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
                  kubernetes: "{ComponentVersions.Kubernetes}"

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
                _logger.LogWarning("RKM could not install or upgrade the Helm charts into the cluster, which could cause the cluster to be inoperable. RKM will continue to ensure that the API server runs to ensure manual recovery is possible.");
            }

            // Flag once the RKM components have been provisioned.
            context.SetFlag(WellKnownFlags.HelmChartProvisioned);
        }
    }
}
