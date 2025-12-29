
using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionClientIdentifier : DhcpOptionBase
{
    private DhcpMessage.THardwareType _hardwareType;
    private byte[] _data;

    public DhcpMessage.THardwareType HardwareType
    {
        get { return _hardwareType; }
        set { _hardwareType = value; }
    }

    public byte[] Data
    {
        get { return _data; }
        set { _data = value; }
    }

    #region IDHCPOption Members

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionClientIdentifier result = new DhcpOptionClientIdentifier();
        _hardwareType = (DhcpMessage.THardwareType)ParseHelper.ReadUInt8(s);
        result._data = new byte[s.Length - s.Position];
        s.ReadExactly(result._data);
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteUInt8(s, (byte)_hardwareType);
        s.Write(_data, 0, _data.Length);
    }

    #endregion

    public DhcpOptionClientIdentifier()
        : base(DhcpOptionType.ClientIdentifier)
    {
        _hardwareType = DhcpMessage.THardwareType.Unknown;
        _data = [];
    }

    public DhcpOptionClientIdentifier(DhcpMessage.THardwareType hardwareType, byte[] data)
        : base(DhcpOptionType.ClientIdentifier)
    {
        _hardwareType = hardwareType;
        _data = data;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],htype=[{_hardwareType}],value=[{Utils.BytesToHexString(_data, " ")}])";
    }
}
