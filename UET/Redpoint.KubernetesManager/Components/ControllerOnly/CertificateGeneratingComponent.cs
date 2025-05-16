namespace Redpoint.KubernetesManager.Components.ControllerOnly
{
    using Redpoint.KubernetesManager.Implementations;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;

    /// <summary>
    /// The certificate generating component generates the certificate authority
    /// on the controller and all the required certificates for other components,
    /// as well as the certificate for the controller to act as a node.
    /// 
    /// It will automatically re-generate any missing certificates at startup and 
    /// then it will raise the <see cref="WellKnownFlags.CertificatesReady"/> flag.
    /// 
    /// This component only runs on the controller. For the component that restores
    /// certificates from the node manifest on nodes, refer to
    /// <see cref="NodeOnly.NodeManifestExpanderComponent"/>.
    /// </summary>
    internal class CertificateGeneratingComponent : IComponent
    {
        private readonly ILogger<CertificateGeneratingComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly ICertificateGenerator _certificateGenerator;
        private readonly ILocalEthernetInfo _localEthernetInfo;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IWslDistro _wslDistro;
        private readonly IWslTranslation _wslTranslation;

        public CertificateGeneratingComponent(
            ILogger<CertificateGeneratingComponent> logger,
            IPathProvider pathProvider,
            ICertificateGenerator certificateGenerator,
            ILocalEthernetInfo localEthernetInfo,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IWslDistro wslDistro,
            IWslTranslation wslTranslation)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _certificateGenerator = certificateGenerator;
            _localEthernetInfo = localEthernetInfo;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _wslDistro = wslDistro;
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
            // In order to use GetWslDistroIPAddress and GetTranslatedIPAddress below,
            // we must wait for networking to be ready. We could technically limit this flag
            // check to Windows only, but I think it's reasonable to wait on all platforms.
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);

            // Generate the certificates where needed.
            var certsPath = Path.Combine(_pathProvider.RKMRoot, "certs");
            Directory.CreateDirectory(certsPath);

            var caPath = Path.Combine(certsPath, "ca");
            var caPemPath = Path.Combine(caPath, "ca.pem");
            var caKeyPath = Path.Combine(caPath, "ca.key");

            ExportedCertificate certificateAuthority;
            if (!File.Exists(caPemPath) || !File.Exists(caKeyPath))
            {
                _logger.LogInformation("Generating certificate authority files...");
                certificateAuthority = await _certificateGenerator.GenerateCertificateAuthorityAsync(cancellationToken);
                Directory.CreateDirectory(caPath);
                await File.WriteAllTextAsync(caPemPath, certificateAuthority.CertificatePem, cancellationToken);
                await File.WriteAllTextAsync(caKeyPath, certificateAuthority.PrivateKeyPem, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Certificate authority already exists.");
                certificateAuthority = new ExportedCertificate(
                    (await File.ReadAllTextAsync(caPemPath, cancellationToken)).Trim(),
                    (await File.ReadAllTextAsync(caKeyPath, cancellationToken)).Trim());
            }

            // We don't get the translated name here because we'll generate certificates for
            // both Windows and WSL if necessary.
            var nodeName = Environment.MachineName.ToLowerInvariant();

            var publicAddress = await _wslTranslation.GetTranslatedIPAddress(cancellationToken);

            var requirements = new List<CertificateRequirement>
            {
                new CertificateRequirement
                {
                    Category = "users",
                    FilenameWithoutExtension = "user-admin",
                    CommonName = "admin",
                    Role = "system:masters",
                },
                new CertificateRequirement
                {
                    Category = "nodes",
                    FilenameWithoutExtension = $"node-{nodeName}",
                    CommonName = $"system:node:{nodeName}",
                    Role = "system:nodes",
                    AdditionalSubjectNames = new[]
                    {
                        nodeName,
                        // This node is the Windows kubelet, so we actually want the Windows IP address
                        // in this case.
                        _localEthernetInfo.IPAddress.ToString()
                    }
                },
                new CertificateRequirement
                {
                    Category = "components",
                    FilenameWithoutExtension = "component-kube-controller-manager",
                    CommonName = "system:kube-controller-manager",
                    Role = "system:kube-controller-manager"
                },
                new CertificateRequirement
                {
                    Category = "components",
                    FilenameWithoutExtension = "component-kube-proxy",
                    CommonName = "system:kube-proxy",
                    Role = "system:kube-proxier"
                },
                new CertificateRequirement
                {
                    Category = "components",
                    FilenameWithoutExtension = "component-kube-scheduler",
                    CommonName = "system:kube-scheduler",
                    Role = "system:kube-scheduler"
                },
                new CertificateRequirement
                {
                    Category = "cluster",
                    FilenameWithoutExtension = "cluster-kubernetes",
                    CommonName = "kubernetes",
                    Role = "Kubernetes",
                    AdditionalSubjectNames = new[]
                    {
                        _clusterNetworkingConfiguration.KubernetesAPIServerIP,
                        // This certificate is used by the API server, potentially inside WSL, so
                        // we want the translated IP address here.
                        publicAddress.ToString(),
                        "127.0.0.1",
                        "kubernetes",
                        "kubernetes.default",
                        "kubernetes.default.svc",
                        "kubernetes.default.svc.cluster",
                        $"kubernetes.svc.{_clusterNetworkingConfiguration.ClusterDNSDomain}"
                    }
                },
                new CertificateRequirement
                {
                    Category = "svc",
                    FilenameWithoutExtension = "svc-service-account",
                    CommonName = "service-accounts",
                    Role = "Kubernetes",
                }
            };
            if (OperatingSystem.IsWindows())
            {
                // On Windows, we also run the kubelet inside WSL, so create a certificate
                // for that separate node as well.
                requirements.Add(
                    new CertificateRequirement
                    {
                        Category = "nodes",
                        FilenameWithoutExtension = $"node-{nodeName}-wsl",
                        CommonName = $"system:node:{nodeName}-wsl",
                        Role = "system:nodes",
                        AdditionalSubjectNames = new[]
                        {
                            $"{nodeName}-wsl",
                            (await _wslDistro.GetWslDistroIPAddress(cancellationToken))!.ToString(),
                        }
                    }
                );
            }

            foreach (var requirement in requirements)
            {
                var path = Path.Combine(certsPath, requirement.Category!);
                var pemPath = Path.Combine(path, $"{requirement.FilenameWithoutExtension}.pem");
                var keyPath = Path.Combine(path, $"{requirement.FilenameWithoutExtension}.key");

                if (!File.Exists(pemPath) || !File.Exists(keyPath))
                {
                    _logger.LogInformation($"Generating certificate: {requirement.Category}/{requirement.FilenameWithoutExtension}");
                    var certificate = _certificateGenerator.GenerateCertificate(
                        certificateAuthority,
                        requirement.CommonName!,
                        requirement.Role!,
                        requirement.AdditionalSubjectNames);
                    Directory.CreateDirectory(path);
                    await File.WriteAllTextAsync(pemPath, certificate.CertificatePem, cancellationToken);
                    await File.WriteAllTextAsync(keyPath, certificate.PrivateKeyPem, cancellationToken);
                }
                else
                {
                    _logger.LogInformation($"Certificate already exists: {requirement.Category}/{requirement.FilenameWithoutExtension}");
                }
            }

            // Certificates are now ready on disk.
            context.SetFlag(WellKnownFlags.CertificatesReady, null);
        }
    }
}
