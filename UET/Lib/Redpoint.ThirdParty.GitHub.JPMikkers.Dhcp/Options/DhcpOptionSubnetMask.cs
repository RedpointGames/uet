using System.IO;
using System.Net;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionSubnetMask : DhcpOptionBase
{
    private IPAddress _subnetMask = IPAddress.None;

    #region IDHCPOption Members

    public IPAddress SubnetMask
    {
        get
        {
            return _subnetMask;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionSubnetMask result = new DhcpOptionSubnetMask();
        if(s.Length != 4) throw new IOException("Invalid DHCP option length");
        result._subnetMask = ParseHelper.ReadIPAddress(s);
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteIPAddress(s, _subnetMask);
    }

    #endregion

    public DhcpOptionSubnetMask()
        : base(DhcpOptionType.SubnetMask)
    {
    }

    public DhcpOptionSubnetMask(IPAddress subnetMask)
        : base(DhcpOptionType.SubnetMask)
    {
        _subnetMask = subnetMask;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_subnetMask}])";
    }
}
