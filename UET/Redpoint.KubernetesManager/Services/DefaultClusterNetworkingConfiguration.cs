namespace Redpoint.KubernetesManager.Services
{
    internal class DefaultClusterNetworkingConfiguration : IClusterNetworkingConfiguration
    {
        /// <summary>
        /// The set of addresses used for pods.
        /// </summary>
        public string ClusterCIDR => "10.244.0.0/16";

        /// <summary>
        /// The set of addresses used for services.
        /// </summary>
        public string ServiceCIDR => "10.96.0.0/12";

        /// <summary>
        /// The Kubernetes API server is automatically assigned the
        /// kubernetes internal DNS name and is linked to the first
        /// address in the service CIDR.
        /// </summary>
        public string KubernetesAPIServerIP => "10.96.0.1";

        /// <summary>
        /// The service IP address used by CoreDNS in the cluster.
        /// </summary>
        public string ClusterDNSServiceIP => "10.96.0.53";

        /// <summary>
        /// The cluster domain used by CoreDNS.
        /// </summary>
        public string ClusterDNSDomain => "cluster.local";

        /// <summary>
        /// The VNI to use for the VXLAN network. This must not be changed as
        /// 4096 is the only value that works on Windows nodes.
        /// </summary>
        public int VXLANVNI => 4096;

        /// <summary>
        /// The port to use for the VXLAN network. This must not be changed as
        /// 4789 is the only value that works on Windows nodes.
        /// </summary>
        public ushort VXLANPort => 4789;

        /// <summary>
        /// The name for the HNS network. This must not be changed as Calico
        /// will specifically set it up as 'Calico' on Windows. This property
        /// is not used on Linux.
        /// </summary>
        public string HnsNetworkName => "Calico";

        /// <summary>
        /// The name for the HNS endpoint for kube-proxy. This must not be changed as Calico
        /// will specifically set it up as 'Calico_ep' on Windows. This property
        /// is not used on Linux.
        /// </summary>
        public string HnsEndpointName => "Calico_ep";
    }
}
