using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionOptionOverload : DhcpOptionBase
{
    private byte _overload;

    #region IDHCPOption Members

    public byte Overload
    {
        get
        {
            return _overload;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionOptionOverload result = new DhcpOptionOptionOverload();
        if(s.Length != 1) throw new IOException("Invalid DHCP option length");
        result._overload = (byte)s.ReadByte();
        return result;
    }

    public override void ToStream(Stream s)
    {
        s.WriteByte(_overload);
    }

    #endregion

    public DhcpOptionOptionOverload()
        : base(DhcpOptionType.OptionOverload)
    {
    }

    public DhcpOptionOptionOverload(byte overload)
        : base(DhcpOptionType.OptionOverload)
    {
        _overload = overload;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_overload}])";
    }
}
