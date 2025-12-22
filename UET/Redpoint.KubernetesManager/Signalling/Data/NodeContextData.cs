namespace Redpoint.KubernetesManager.Signalling.Data
{
    using Redpoint.KubernetesManager.Models;
    using System.Net;

    internal class NodeContextData : IAssociatedData
    {
        public NodeContextData(LegacyNodeManifest nodeManifest, IPAddress controllerAddress)
        {
            NodeManifest = nodeManifest;
            ControllerAddress = controllerAddress;
        }

        public LegacyNodeManifest NodeManifest { get; }

        public IPAddress ControllerAddress { get; }
    }
}
