namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;

    /// <summary>
    /// The CoreDNS provisioning component installs or upgrades CoreDNS in the cluster. It waits for the API server to be available so that it can then run Helm.
    /// 
    /// This component only runs on the controller.
    /// </summary>
    internal class CoreDNSProvisioningComponent : IComponent
    {
        private readonly ILogger<CoreDNSProvisioningComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IKubeConfigManager _kubeConfigManager;
        private readonly IProcessExecutor _processExecutor;

        public CoreDNSProvisioningComponent(
            ILogger<CoreDNSProvisioningComponent> logger,
            IPathProvider pathProvider,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IKubeConfigManager kubeConfigManager,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _kubeConfigManager = kubeConfigManager;
            _processExecutor = processExecutor;
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

            // The path to Helm that we extracted earlier.
            var helmPath = Path.Combine(_pathProvider.RKMRoot, "helm", "helm");

            // Install/upgrade CoreDNS via OCI charts.
            var arguments = new List<LogicalProcessArgument>()
            {
                $"--kubeconfig={_kubeConfigManager.GetKubeconfigPath("users", "user-admin")}",
                "--namespace=kube-system",
                "upgrade",
                "--install",
                "coredns",
                "oci://ghcr.io/coredns/charts/coredns",
                "--wait",
                "--set",
                $"service.clusterIP={_clusterNetworkingConfiguration.ClusterDNSServiceIP}",
                "--set-json",
                "nodeSelector={\"kubernetes.io/os\":\"linux\"}",
            };
            if (_clusterNetworkingConfiguration.ClusterDNSDomain != "cluster.local")
            {
                // @note: This is less safe that I would like, since it relies on the order of 'plugins' not
                // changing in the default values array.
                arguments.AddRange(
                [
                    "--set",
                    $"servers[0].plugins[3].parameters={_clusterNetworkingConfiguration.ClusterDNSDomain} in-addr.arpa ip6.arpa",
                ]);
            }
            var exitCode = await _processExecutor.ExecuteAsync(
                new ProcessExecution.ProcessSpecification
                {
                    FilePath = helmPath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(helmPath)!,
                },
                CaptureSpecification.Passthrough,
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
