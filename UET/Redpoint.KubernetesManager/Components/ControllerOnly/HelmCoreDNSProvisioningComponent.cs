namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Services.Helm;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using Redpoint.ProcessExecution;
    using System.Threading.Tasks;

    /// <summary>
    /// The CoreDNS provisioning component installs or upgrades CoreDNS in the
    /// cluster. It waits for the API server to be available so that it can then run Helm.
    /// 
    /// This component only runs on the controller.
    /// </summary>
    internal class HelmCoreDNSProvisioningComponent : IComponent
    {
        private readonly ILogger<HelmCoreDNSProvisioningComponent> _logger;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IHelmDeployment _helmDeployment;

        public HelmCoreDNSProvisioningComponent(
            ILogger<HelmCoreDNSProvisioningComponent> logger,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IHelmDeployment helmDeployment)
        {
            _logger = logger;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _helmDeployment = helmDeployment;
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
            // Deploy CoreDNS for Linux inside the cluster.
            var exitCode = await _helmDeployment.DeployChart(
                context,
                "coredns",
                "oci://ghcr.io/coredns/charts/coredns",
                $"""
                service:
                  clusterIP: "{_clusterNetworkingConfiguration.ClusterDNSServiceIP}"
                nodeSelector:
                  kubernetes.io/os: linux
                servers:
                - zones:
                  - zone: .
                  port: 53
                  plugins:
                  - name: errors
                  - name: health
                    configBlock: |-
                      lameduck 10s
                  - name: ready
                  - name: kubernetes
                    parameters: {_clusterNetworkingConfiguration.ClusterDNSDomain} in-addr.arpa ip6.arpa
                    configBlock: |-
                      pods insecure
                      fallthrough in-addr.arpa ip6.arpa
                      ttl 30
                  - name: prometheus
                    parameters: 0.0.0.0:9153
                  - name: forward
                    parameters: . 1.1.1.1 1.0.0.1
                  - name: cache
                    parameters: 30
                  - name: loop
                  - name: reload
                  - name: loadbalance
                """,
                waitForResourceStabilisation: true,
                cancellationToken);
            if (exitCode != 0)
            {
                _logger.LogCritical("rkm is exiting because it could not deploy CoreDNS via Helm, and CoreDNS is required for networking to work.");
                context.StopOnCriticalError();
                return;
            }

            // CoreDNS has now been provisioned via Helm.
            context.SetFlag(WellKnownFlags.CoreDNSProvisioned);
        }
    }
}
