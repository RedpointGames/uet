namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionNetworkTimeProtocolServers : DhcpOptionServerListBase
{
    public override DhcpOptionServerListBase Create()
    {
        return new DhcpOptionNetworkTimeProtocolServers();
    }

    public DhcpOptionNetworkTimeProtocolServers() : base(DhcpOptionType.NetworkTimeProtocolServers)
    {
    }
}
