namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionRouter : DhcpOptionServerListBase
{
    public override DhcpOptionServerListBase Create()
    {
        return new DhcpOptionRouter();
    }

    public DhcpOptionRouter() : base(DhcpOptionType.Router)
    {
    }
}
