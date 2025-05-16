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
    /// The etcd component sets up and runs the etcd process.
    /// </summary>
    internal class EtcdComponent : IComponent
    {
        private readonly ILogger<EtcdComponent> _logger;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IPathProvider _pathProvider;
        private readonly ICertificateManager _certificateManager;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IWslTranslation _wslTranslation;

        public EtcdComponent(
            ILogger<EtcdComponent> logger,
            ILocalEthernetInfo localEthernetInfo,
            IPathProvider pathProvider,
            ICertificateManager certificateManager,
            IProcessMonitorFactory processMonitorFactory,
            IWslTranslation wslTranslation)
        {
            _logger = logger;
            _localEthernetInfo = localEthernetInfo;
            _pathProvider = pathProvider;
            _certificateManager = certificateManager;
            _processMonitorFactory = processMonitorFactory;
            _wslTranslation = wslTranslation;
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

            var publicAddress = await _wslTranslation.GetTranslatedIPAddress(cancellationToken);

            _logger.LogInformation("Starting etcd and keeping it running...");
            var etcdMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                    filename: _wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "etcd", "etcd")),
                    arguments: new[]
                    {
                        "--name",
                        "kubernetes",
                        $"--cert-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("cluster", "cluster-kubernetes"))}",
                        $"--key-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificateKeyPath("cluster", "cluster-kubernetes"))}",
                        $"--peer-cert-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("cluster", "cluster-kubernetes"))}",
                        $"--peer-key-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificateKeyPath("cluster", "cluster-kubernetes"))}",
                        $"--trusted-ca-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("ca", "ca"))}",
                        $"--peer-trusted-ca-file={_wslTranslation.TranslatePath(_certificateManager.GetCertificatePemPath("ca", "ca"))}",
                        $"--peer-client-cert-auth",
                        $"--client-cert-auth",
                        $"--listen-client-urls",
                        $"https://{publicAddress}:2379,https://127.0.0.1:2379",
                        $"--advertise-client-urls",
                        $"https://{publicAddress}:2379",
                        $"--data-dir={_wslTranslation.TranslatePath(Path.Combine(_pathProvider.RKMRoot, "etcd", "data"))}"
                    },
                    wsl: true));
            await etcdMonitor.RunAsync(cancellationToken);
        }
    }
}
