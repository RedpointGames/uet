namespace Redpoint.KubernetesManager.Components.WslExtra
{
    using Redpoint.KubernetesManager.Implementations;
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Services.Windows;

    /// <summary>
    /// The CoreDNS component runs CoreDNS as a native application on Windows. This
    /// is necessary when Windows is running as a controller as UDP routing does not
    /// seem to work over the virtual switch from the Windows kubelet to the Linux
    /// kubelet, and thus Windows services can't hit the CoreDNS service running inside
    /// Kubernetes. This works around the issue by having the Windows side of things
    /// run it's own copy of CoreDNS.
    /// </summary>
    internal class CoreDNSComponent : IComponent
    {
        private readonly ILogger<CoreDNSComponent> _logger;
        private readonly IPathProvider _pathProvider;
        private readonly IResourceManager _resourceManager;
        private readonly ICertificateManager _certificateManager;
        private readonly IClusterNetworkingConfiguration _clusterNetworkingConfiguration;
        private readonly IProcessMonitorFactory _processMonitorFactory;
        private readonly IWslTranslation _wslTranslation;

        public CoreDNSComponent(
            ILogger<CoreDNSComponent> logger,
            IPathProvider pathProvider,
            IResourceManager resourceManager,
            ICertificateManager certificateManager,
            IClusterNetworkingConfiguration clusterNetworkingConfiguration,
            IProcessMonitorFactory processMonitorFactory,
            IWslTranslation wslTranslation)
        {
            _logger = logger;
            _pathProvider = pathProvider;
            _resourceManager = resourceManager;
            _certificateManager = certificateManager;
            _clusterNetworkingConfiguration = clusterNetworkingConfiguration;
            _processMonitorFactory = processMonitorFactory;
            _wslTranslation = wslTranslation;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Controller && OperatingSystem.IsWindows())
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            await context.WaitForFlagAsync(WellKnownFlags.AssetsReady);
            await context.WaitForFlagAsync(WellKnownFlags.OSNetworkingReady);
            await context.WaitForFlagAsync(WellKnownFlags.CertificatesReady);
            await context.WaitForFlagAsync(WellKnownFlags.KubeApiServerReady);

            _logger.LogInformation("Setting up CoreDNS configuration...");
            await _resourceManager.ExtractResource(
                "Corefile-windows",
                Path.Combine(_pathProvider.RKMRoot, "coredns", "Corefile"),
                new Dictionary<string, string>
                {
                    { "__CLUSTER_DOMAIN__", _clusterNetworkingConfiguration.ClusterDNSDomain },
                    { "__KUBERNETES_ENDPOINT__", $"https://{await _wslTranslation.GetTranslatedIPAddress(cancellationToken)}:6443" },
                    // @todo: This should really have it's own certificate, but creating a certificate
                    // with the role "system:serviceaccount:kube-system:coredns" didn't seem to work, so we're just using user-admin
                    // for now (which has full permissions).
                    { "__CERT__", _certificateManager.GetCertificatePemPath("users", "user-admin") },
                    { "__KEY__", _certificateManager.GetCertificateKeyPath("users", "user-admin") },
                    { "__CA__", _certificateManager.GetCertificatePemPath("ca", "ca") },
                });

            _logger.LogInformation("Starting CoreDNS and keeping it running...");
            var containerdMonitor = _processMonitorFactory.CreatePerpetualProcess(new ProcessSpecification(
                filename: Path.Combine(_pathProvider.RKMRoot, "coredns", "coredns.exe"),
                arguments: new[]
                {
                    "-conf",
                    Path.Combine(_pathProvider.RKMRoot, "coredns", "Corefile"),
                    "-v",
                    "2"
                }));
            await containerdMonitor.RunAsync(cancellationToken);
        }
    }
}
