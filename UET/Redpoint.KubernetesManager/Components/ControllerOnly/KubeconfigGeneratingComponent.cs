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
    /// It will automatically re-generate any missing kubeconfigs at startup, 
    /// and then it will raise the <see cref="WellKnownFlags.KubeconfigsReady"/> flag.
    /// 
    /// This component waits for the <see cref="WellKnownFlags.CertificatesReady"/>
    /// flag to be set by <see cref="CertificateGeneratingComponent"/>, as the
    /// certificates are required to generate kubeconfigs.
    /// 
    /// This component only runs on the controller. For the component that restores
    /// kubeconfigs from the node manifest on nodes, refer to
    /// <see cref="NodeOnly.NodeManifestExpanderComponent"/>.
    /// </summary>
    internal class KubeconfigGeneratingComponent : IComponent
    {
        private readonly ILogger<KubeconfigGeneratingComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IKubeconfigGenerator _kubeconfigGenerator;

        public KubeconfigGeneratingComponent(
            ILogger<KubeconfigGeneratingComponent> logger,
            IPathProvider pathProvider,
            IKubeconfigGenerator kubeConfigGenerator)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _kubeconfigGenerator = kubeConfigGenerator;
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

            var kubeconfigsToRequirements = new Dictionary<string, CertificateRequirement>
            {
                {
                    "users/user-admin",
                    new CertificateRequirement
                    {
                        Category = "users",
                        FilenameWithoutExtension = "user-admin",
                        CommonName = "admin",
                        Role = "system:masters",
                    }
                },
                {
                    "components/component-kube-controller-manager",
                    new CertificateRequirement
                    {
                        Category = "components",
                        FilenameWithoutExtension = "component-kube-controller-manager",
                        CommonName = "system:kube-controller-manager",
                        Role = "system:kube-controller-manager"
                    }
                },
                {
                    "components/component-kube-scheduler",
                    new CertificateRequirement
                    {
                        Category = "components",
                        FilenameWithoutExtension = "component-kube-scheduler",
                        CommonName = "system:kube-scheduler",
                        Role = "system:kube-scheduler"
                    }
                }
            };

            var kubeconfigsPath = Path.Combine(_pathProvider.RKMRoot, "kubeconfigs");
            foreach (var (kubeconfig, certificateRequirement) in kubeconfigsToRequirements)
            {
                var kubeconfigPath = Path.Combine(kubeconfigsPath, kubeconfig + ".kubeconfig");

                // Always refresh kubeconfigs.
                Directory.CreateDirectory(Path.GetDirectoryName(kubeconfigPath)!);
                _logger.LogInformation($"Generating kubeconfig: {kubeconfig}");
                var split = kubeconfig.Split('/');
                await File.WriteAllTextAsync(
                    kubeconfigPath,
                    await _kubeconfigGenerator.GenerateKubeconfigOnController(
                        certificateRequirement,
                        cancellationToken),
                    cancellationToken);
            }

            // Kubeconfigs are now ready on disk.
            context.SetFlag(WellKnownFlags.KubeconfigsReady, null);
        }
    }
}
