using System.IO;
using System.Net;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionServerIdentifier : DhcpOptionBase
{
    private IPAddress _IPAddress = IPAddress.None;

    public IPAddress IPAddress
    {
        get
        {
            return _IPAddress;
        }
    }

    #region IDHCPOption Members

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionServerIdentifier result = new DhcpOptionServerIdentifier();
        if(s.Length != 4) throw new IOException("Invalid DHCP option length");
        result._IPAddress = ParseHelper.ReadIPAddress(s);
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteIPAddress(s, _IPAddress);
    }

    #endregion

    public DhcpOptionServerIdentifier()
        : base(DhcpOptionType.ServerIdentifier)
    {
    }

    public DhcpOptionServerIdentifier(IPAddress ipAddress)
        : base(DhcpOptionType.ServerIdentifier)
    {
        _IPAddress = ipAddress;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_IPAddress}])";
    }
}
