namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionDomainNameServer : DhcpOptionServerListBase
{
    public override DhcpOptionServerListBase Create()
    {
        return new DhcpOptionDomainNameServer();
    }

    public DhcpOptionDomainNameServer() : base(DhcpOptionType.DomainNameServer)
    {
    }
}
