namespace Redpoint.KubernetesManager.Signalling.Data
{
    internal class NodeNameContextData : IAssociatedData
    {
        public string NodeName { get; }

        public NodeNameContextData(string nodeName)
        {
            NodeName = nodeName;
        }
    }
}
