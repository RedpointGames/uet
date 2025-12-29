using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionMaximumDhcpMessageSize : DhcpOptionBase
{
    private ushort _maxSize;

    #region IDHCPOption Members

    public ushort MaxSize
    {
        get
        {
            return _maxSize;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionMaximumDhcpMessageSize result = new DhcpOptionMaximumDhcpMessageSize();
        if(s.Length != 2) throw new IOException("Invalid DHCP option length");
        result._maxSize = ParseHelper.ReadUInt16(s);
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteUInt16(s, _maxSize);
    }

    #endregion

    public DhcpOptionMaximumDhcpMessageSize()
        : base(DhcpOptionType.MaximumDHCPMessageSize)
    {
    }

    public DhcpOptionMaximumDhcpMessageSize(ushort maxSize)
        : base(DhcpOptionType.MaximumDHCPMessageSize)
    {
        _maxSize = maxSize;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_maxSize}])";
    }
}
