namespace Redpoint.KubernetesManager.Signalling
{
    internal enum RoleType
    {
        /// <summary>
        /// This instance will run the controller (and optionally a node).
        /// </summary>
        Controller,

        /// <summary>
        /// This instance is only a node.
        /// </summary>
        Node,
    }
}
