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
    /// The Kubernetes API server component sets up and runs the kube-apiserver process.
    /// </summary>
    internal class KubernetesApiServerComponent : IComponent
    {
        private readonly ILogger<KubernetesApiServerComponent> _logger;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IPathProvider _pathProvider;
        private readonly ICertificateManager _certificateManager;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IWslTranslation _wslTranslation;
        private readonly IEncryptionConfigManager _encryptionConfigManager;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;

        public KubernetesApiServerComponent(
            ILogger<KubernetesApiServerComponent> logger,
            ILocalEthernetInfo localEthernetInfo,
            IPathProvider pathProvider,
            ICertificateManager certificateManager,
            IProcessMonitorFactory processMonitorFactory,
            IWslTranslation wslTranslation,
            IEncryptionConfigManager encryptionConfigManager,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration)
        {
            _logger = logger;
            _localEthernetInfo = localEthernetInfo;
            _pathProvider = pathProvider;
            _certificateManager = certificateManager;
            _processMonitorFactory = processMonitorFactory;
            _wslTranslation = wslTranslation;
            _encryptionConfigManager = encryptionConfigManager;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
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
            // Wait for the operating system network so we can safely use GetTranslatedIPAddress.
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
            await context.WaitForFlagAsync(WellKnownFlags.CertificatesReady);
            await context.WaitForFlagAsync(WellKnownFlags.EncryptionConfigReady);

            var publicAddress = await _wslTranslation.GetTranslatedIPAddress(cancellationToken);

            _logger.LogInformation("Starting kube-apiserver and keeping it running...");
            var kubeMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                    filename: _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "kubernetes-server", "kubernetes", "server", "bin", "kube-apiserver")),
                    arguments: new[]
                    {
                        $"--advertise-address={publicAddress}",
                        $"--allow-privileged=true",
                        $"--apiserver-count=1",
                        $"--audit-log-maxage=30",
                        $"--audit-log-maxbackup=3",
                        $"--audit-log-maxsize=100",
                        $"--audit-log-path={_wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "logs", "audit.log"))}",
                        $"--authorization-mode=Node,RBAC",
                        $"--bind-address=0.0.0.0",
                        $"--client-ca-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("ca", "ca"))}",
                        $"--enable-admission-plugins=NamespaceLifecycle,NodeRestriction,LimitRanger,ServiceAccount,DefaultStorageClass,ResourceQuota",
                        $"--etcd-cafile={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("ca", "ca"))}",
                        $"--etcd-certfile={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("cluster", "cluster-kubernetes"))}",
                        $"--etcd-keyfile={_wslTranslation.TranslatePath(_certificateManager.GetCertificateKeyPath("cluster", "cluster-kubernetes"))}",
                        $"--etcd-servers=https://{publicAddress}:2379",
                        $"--event-ttl=1h",
                        $"--encryption-provider-config={_wslTranslation.TranslatePath(_encryptionConfigManager.EncryptionConfigPath)}",
                        $"--kubelet-certificate-authority={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("ca", "ca"))}",
                        $"--kubelet-client-certificate={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("cluster", "cluster-kubernetes"))}",
                        $"--kubelet-client-key={_wslTranslation.TranslatePath(_certificateManager.GetCertificateKeyPath("cluster", "cluster-kubernetes"))}",
                        $"--runtime-config=api/all=true",
                        $"--service-account-key-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("svc", "svc-service-account"))}",
                        $"--service-account-signing-key-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificateKeyPath("svc", "svc-service-account"))}",
                        $"--service-account-issuer=https://{publicAddress}:6443",
                        $"--service-cluster-ip-range={_wslTranslation.TranslatePath(_clusterNetworkingConfiguration.ServiceCIDR)}",
                        $"--service-node-port-range=30000-32767",
                        $"--tls-cert-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("cluster", "cluster-kubernetes"))}",
                        $"--tls-private-key-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificateKeyPath("cluster", "cluster-kubernetes"))}",
                        $"--v=2"
                    },
                    wsl: true));
            var kubeTask = kubeMonitor.RunAsync(cancellationToken);
            context.SetFlag(WellKnownFlags.KubeApiServerStarted);
            await kubeTask;
        }
    }
}
