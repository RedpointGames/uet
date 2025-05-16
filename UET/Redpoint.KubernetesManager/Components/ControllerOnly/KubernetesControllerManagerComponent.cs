namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Components;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;

    /// <summary>
    /// The Kubernetes controller manager component sets up and runs the kube-controller-manager process.
    /// </summary>
    internal class KubernetesControllerManagerComponent : IComponent
    {
        private readonly ILogger<KubernetesControllerManagerComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly ICertificateManager _certificateManager;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IWslTranslation _wslTranslation;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IKubeConfigManager _kubeConfigManager;

        public KubernetesControllerManagerComponent(
            ILogger<KubernetesControllerManagerComponent> logger,
            IPathProvider pathProvider,
            ICertificateManager certificateManager,
            IProcessMonitorFactory processMonitorFactory,
            IWslTranslation wslTranslation,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IKubeConfigManager kubeConfigManager)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _certificateManager = certificateManager;
            _processMonitorFactory = processMonitorFactory;
            _wslTranslation = wslTranslation;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _kubeConfigManager = kubeConfigManager;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        public async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
            await context.WaitForFlagAsync(WellKnownFlags.CertificatesReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeConfigsReady);
            // Not a strict dependency, but doesn't make sense to start running this process until the API server is also started.
            await context.WaitForFlagAsync(WellKnownFlags.KubeApiServerStarted);

            _logger.LogInformation("Starting kube-controller-manager and keeping it running...");
            var kubeMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                    filename: _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "kubernetes-server", "kubernetes", "server", "bin", "kube-controller-manager")),
                    arguments: new[]
                    {
                        $"--bind-address=0.0.0.0",
                        $"--cluster-cidr={_clusterNetworkingConfiguration.ClusterCIDR}",
                        $"--cluster-name=kubernetes",
                        $"--cluster-signing-cert-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("cluster", "cluster-kubernetes"))}",
                        $"--cluster-signing-key-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificateKeyPath("cluster", "cluster-kubernetes"))}",
                        $"--kubeconfig={_wslTranslation.TranslatePath(_kubeConfigManager.GetKubeconfigPath("components", "component-kube-controller-manager"))}",
                        $"--root-ca-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("ca", "ca"))}",
                        $"--service-account-private-key-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificateKeyPath("svc", "svc-service-account"))}",
                        $"--service-cluster-ip-range={_clusterNetworkingConfiguration.ServiceCIDR}",
                        $"--use-service-account-credentials=true",
                        $"--v=2"
                    },
                    wsl: true));
            await kubeMonitor.RunAsync(cancellationToken);
        }
    }
}
