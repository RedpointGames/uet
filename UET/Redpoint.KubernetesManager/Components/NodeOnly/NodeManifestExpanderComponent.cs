namespace Redpoint.KubernetesManager.Components.NodeOnly
{
    using Redpoint.KubernetesManager.Services;
    using Redpoint.KubernetesManager.Signalling;
    using Redpoint.KubernetesManager.Signalling.Data;
    using System.Threading.Tasks;

    /// <summary>
    /// The node manifest expander component takes the node manifest issued
    /// by the RKM controller and expands it's content to all of the separate
    /// files on disk
    /// </summary>
    internal class NodeManifestExpanderComponent : IComponent
    {
        private readonly IPathProvider _pathProvider;

        public NodeManifestExpanderComponent(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
        }

        public void RegisterSignals(IRegistrationContext context)
        {
            if (context.Role == RoleType.Node)
            {
                context.OnSignal(WellKnownSignals.Started, OnStartedAsync);
            }
        }

        private async Task OnStartedAsync(IContext context, IAssociatedData? data, CancellationToken cancellationToken)
        {
            // Wait for the node manifest to be available.
            var nodeContext = await context.WaitForFlagAsync<NodeContextData>(WellKnownFlags.NodeContextAvailable);
            var nodeManifest = nodeContext.NodeManifest;
            var controllerAddress = nodeContext.ControllerAddress;

            // Write out the files from the node manifest into the locations that processes will expect them. When the controller
            // ships configuration files to clients, it sets the addresses as __CONTROLLER_ADDRESS__, which allows the node to replace
            // the controller address with the address it sees (rather than the controller having to figure out what address it is projecting).
            Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "certs", "ca"));
            Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "certs", "nodes"));
            await File.WriteAllTextAsync(
                Path.Combine(_pathProvider.RKMRoot, "certs", "ca", $"ca.pem"),
                nodeManifest.CertificateAuthority,
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(_pathProvider.RKMRoot, "certs", "nodes", $"node-{nodeManifest.NodeName}.pem"),
                nodeManifest.NodeCertificate,
                cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(_pathProvider.RKMRoot, "certs", "nodes", $"node-{nodeManifest.NodeName}.key"),
                nodeManifest.NodeCertificateKey,
                cancellationToken);
            Directory.CreateDirectory(Path.Combine(_pathProvider.RKMRoot, "kubeconfigs", "nodes"));
            await File.WriteAllTextAsync(
                Path.Combine(_pathProvider.RKMRoot, "kubeconfigs", "nodes", $"node-{nodeManifest.NodeName}.kubeconfig"),
                nodeManifest.NodeKubeletConfig.Replace("__CONTROLLER_ADDRESS__", controllerAddress.ToString(), StringComparison.Ordinal),
                cancellationToken);

            // On Linux nodes, we have to symlink the server's installation root to our own installation root, because
            // calico requires paths to be the same on every machine.
            if (OperatingSystem.IsLinux())
            {
                if (!File.Exists(Path.Combine(_pathProvider.RKMRoot, "..", nodeManifest.ServerRKMInstallationId)) &&
                    !Directory.Exists(Path.Combine(_pathProvider.RKMRoot, "..", nodeManifest.ServerRKMInstallationId)))
                {
                    File.CreateSymbolicLink(
                        Path.Combine(_pathProvider.RKMRoot, "..", nodeManifest.ServerRKMInstallationId),
                        _pathProvider.RKMRoot);
                }
            }

            // Certificates and kubeconfigs are now ready on disk.
            context.SetFlag(WellKnownFlags.CertificatesReady);
            context.SetFlag(WellKnownFlags.KubeConfigsReady);

            // The worker node components are now ready to start.
            context.SetFlag(WellKnownFlags.NodeComponentsReadyToStart, new NodeNameContextData(nodeManifest.NodeName));
        }
    }
}
