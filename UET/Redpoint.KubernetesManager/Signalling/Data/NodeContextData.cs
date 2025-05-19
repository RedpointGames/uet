namespace Redpoint.KubernetesManager.Signalling.Data
{
    using Redpoint.KubernetesManager.Models;
    using System.Net;

    internal class NodeContextData : IAssociatedData
    {
        public NodeContextData(NodeManifest nodeManifest, IPAddress controllerAddress)
        {
            NodeManifest = nodeManifest;
            ControllerAddress = controllerAddress;
        }

        public NodeManifest NodeManifest { get; }

        public IPAddress ControllerAddress { get; }
    }
}
