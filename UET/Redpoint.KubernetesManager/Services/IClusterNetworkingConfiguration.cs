namespace Redpoint.KubernetesManager.Services
{
    internal interface IClusterNetworkingConfiguration
    {
        string ClusterCIDR { get; }

        string ServiceCIDR { get; }

        string KubernetesAPIServerIP { get; }

        string ClusterDNSServiceIP { get; }

        string ClusterDNSDomain { get; }

        int VXLANVNI { get; }

        ushort VXLANPort { get; }

        string HnsNetworkName { get; }

        string HnsEndpointName { get; }
    }
}
