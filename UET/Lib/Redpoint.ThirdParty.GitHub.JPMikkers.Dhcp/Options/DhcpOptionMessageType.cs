using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionMessageType : DhcpOptionBase
{
    private DhcpOptionMessageTypeType _messageType;

    #region IDHCPOption Members

    public DhcpOptionMessageTypeType MessageType
    {
        get
        {
            return _messageType;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionMessageType result = new DhcpOptionMessageType();
        if (s.Length != 1) throw new IOException("Invalid DHCP option length");
        result._messageType = (DhcpOptionMessageTypeType)s.ReadByte();
        return result;
    }

    public override void ToStream(Stream s)
    {
        s.WriteByte((byte)_messageType);
    }

    #endregion

    public DhcpOptionMessageType()
        : base(DhcpOptionType.MessageType)
    {
    }

    public DhcpOptionMessageType(DhcpOptionMessageTypeType messageType)
        : base(DhcpOptionType.MessageType)
    {
        _messageType = messageType;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_messageType}])";
    }
}
