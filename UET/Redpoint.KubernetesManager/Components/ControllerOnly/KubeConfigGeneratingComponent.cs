namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.KubernetesManager.Services.Wsl;

    /// <summary>
    /// The kubeconfig generating component generates the kubeconfigs on the
    /// controller, for all of the Kubernetes components.
    /// 
    /// It will automatically re-generate any missing kubeconfigs at startup
    /// using the <see cref="IKubeConfigManager"/> interface, and then it will
    /// raise the <see cref="WellKnownFlags.KubeConfigsReady"/> flag.
    /// 
    /// This component waits for the <see cref="WellKnownFlags.CertificatesReady"/>
    /// flag to be set by <see cref="CertificateGeneratingComponent"/>, as the
    /// certificates are required to generate kubeconfigs.
    /// 
    /// This component only runs on the controller. For the component that restores
    /// kubeconfigs from the node manifest on nodes, refer to
    /// <see cref="NodeOnly.NodeManifestExpanderComponent"/>.
    /// </summary>
    internal class KubeConfigGeneratingComponent : IComponent
    {
        private readonly ILogger<KubeConfigGeneratingComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly ICertificateManager _certificateManager;
        private readonly IKubeConfigGenerator _kubeConfigGenerator;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IWslTranslation _wslTranslation;

        public KubeConfigGeneratingComponent(
            ILogger<KubeConfigGeneratingComponent> logger,
            IPathProvider pathProvider,
            ICertificateManager certificateManager,
            IKubeConfigGenerator kubeConfigGenerator,
            ILocalEthernetInfo localEthernetInfo,
            IWslTranslation wslTranslation)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _certificateManager = certificateManager;
            _kubeConfigGenerator = kubeConfigGenerator;
            _localEthernetInfo = localEthernetInfo;
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
            // Wait for the operating system network so we can safely use GetTranslatedIPAddress.
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            // Wait for the certificates to be available.
            await context.WaitForFlagAsync(WellKnownFlags.CertificatesReady);

            // Generate the kubeconfigs where needed. We don't get the translated hostname here
            // because we'll generate kubeconfigs for both Windows and WSL if needed.
            var nodeName = Environment.MachineName.ToLowerInvariant();

            var kubeconfigs = new List<string>
            {
                $"nodes/node-{nodeName}",
                "components/component-kube-proxy",
                "components/component-kube-controller-manager",
                "components/component-kube-scheduler",
                "users/user-admin",
            };
            if (OperatingSystem.IsWindows())
            {
                kubeconfigs.AddRange(new[]
                {
                    // For the kubelet that runs inside WSL.
                    $"nodes/node-{nodeName}-wsl",
                });
            }

            var certificateAuthorityPem = await File.ReadAllTextAsync(_certificateManager.GetCertificatePemPath("ca", "ca"), cancellationToken);

            var publicAddress = await _wslTranslation.GetTranslatedIPAddress(cancellationToken);

            var kubeconfigsPath = Path.Combine(_pathProvider.RKMRoot, "kubeconfigs");
            foreach (var kubeconfig in kubeconfigs)
            {
                var kubeconfigPath = Path.Combine(kubeconfigsPath, kubeconfig + ".kubeconfig");
                if (!File.Exists(kubeconfigPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(kubeconfigPath)!);
                    _logger.LogInformation($"Generating kubeconfig: {kubeconfig}");
                    var split = kubeconfig.Split('/');
                    await File.WriteAllTextAsync(
                        kubeconfigPath,
                        _kubeConfigGenerator.GenerateKubeConfig(
                            certificateAuthorityPem,
                            publicAddress.ToString(),
                            new ExportedCertificate(
                                await File.ReadAllTextAsync(_certificateManager.GetCertificatePemPath(split[0], split[1]), cancellationToken),
                                await File.ReadAllTextAsync(_certificateManager.GetCertificateKeyPath(split[0], split[1]), cancellationToken))),
                        cancellationToken);
                }
                else
                {
                    _logger.LogInformation($"Kubeconfig already exists: {kubeconfig}");
                }
            }

            // Kubeconfigs are now ready on disk.
            context.SetFlag(WellKnownFlags.KubeConfigsReady, null);
        }
    }
}
