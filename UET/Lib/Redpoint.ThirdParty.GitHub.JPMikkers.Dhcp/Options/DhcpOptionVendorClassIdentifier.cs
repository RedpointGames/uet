using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionVendorClassIdentifier : DhcpOptionBase
{
    private byte[] _data;

    public byte[] Data
    {
        get { return _data; }
        set { _data = value; }
    }

    #region IDHCPOption Members

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionVendorClassIdentifier result = new DhcpOptionVendorClassIdentifier();
        result._data = new byte[s.Length];
        s.ReadExactly(result._data);
        return result;
    }

    public override void ToStream(Stream s)
    {
        s.Write(_data, 0, _data.Length);
    }

    #endregion

    public DhcpOptionVendorClassIdentifier()
        : base(DhcpOptionType.VendorClassIdentifier)
    {
        _data = new byte[0];
    }

    public DhcpOptionVendorClassIdentifier(byte[] data)
        : base(DhcpOptionType.VendorClassIdentifier)
    {
        _data = data;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{Utils.BytesToHexString(_data, " ")}])";
    }
}
