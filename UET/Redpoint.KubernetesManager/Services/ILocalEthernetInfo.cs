namespace Redpoint.KubernetesManager.Services
{
    using System.Net;
    using System.Net.NetworkInformation;

    internal interface ILocalEthernetInfo
    {
        bool HasIPAddress { get; }

        IPAddress IPAddress { get; }

        bool IsLoopbackAddress(IPAddress address);

        NetworkInterface? NetworkAdapter { get; }

        string? HostSubnetCIDR { get; }
    }
}
