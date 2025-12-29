using System.IO;
using System.Net;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionRequestedIPAddress : DhcpOptionBase
{
    private IPAddress _IPAddress = IPAddress.None;

    #region IDHCPOption Members

    public IPAddress IPAddress
    {
        get
        {
            return _IPAddress;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionRequestedIPAddress result = new DhcpOptionRequestedIPAddress();
        if(s.Length != 4) throw new IOException("Invalid DHCP option length");
        result._IPAddress = ParseHelper.ReadIPAddress(s);
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteIPAddress(s, _IPAddress);
    }

    #endregion

    public DhcpOptionRequestedIPAddress()
        : base(DhcpOptionType.RequestedIPAddress)
    {
    }

    public DhcpOptionRequestedIPAddress(IPAddress ipAddress)
        : base(DhcpOptionType.RequestedIPAddress)
    {
        _IPAddress = ipAddress;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_IPAddress}])";
    }
}
