namespace Redpoint.KubernetesManager.Components.NodeOnly
{
    using Redpoint.KubernetesManager.Abstractions;
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

            // @note: We no longer write out files here. ManifestServerComponent pulls the contents directly from the node manifest
            // in the NodeContextData, and places them in the KubeletManifest for the Kubelet service to write to disk as needed.

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
            context.SetFlag(WellKnownFlags.KubeconfigsReady);

            // The worker node components are now ready to start.
            context.SetFlag(WellKnownFlags.NodeComponentsReadyToStart, new NodeNameContextData(nodeManifest.NodeName));
        }
    }
}
