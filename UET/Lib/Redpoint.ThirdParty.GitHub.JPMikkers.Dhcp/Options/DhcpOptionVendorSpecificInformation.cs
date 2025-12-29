using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionVendorSpecificInformation : DhcpOptionBase
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
        DhcpOptionVendorSpecificInformation result = new DhcpOptionVendorSpecificInformation();
        result._data = new byte[s.Length];
        s.ReadExactly(result._data);
        return result;
    }

    public override void ToStream(Stream s)
    {
        s.Write(_data, 0, _data.Length);
    }

    #endregion

    public DhcpOptionVendorSpecificInformation()
        : base(DhcpOptionType.VendorSpecificInformation)
    {
        _data = [];
    }

    public DhcpOptionVendorSpecificInformation(byte[] data)
        : base(DhcpOptionType.VendorSpecificInformation)
    {
        _data = data;
    }

    public DhcpOptionVendorSpecificInformation(string data)
        : base(DhcpOptionType.VendorSpecificInformation)
    {
        MemoryStream ms = new MemoryStream();
        ParseHelper.WriteString(ms, ZeroTerminatedStrings, data);
        ms.Flush();
        _data = ms.ToArray();
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{Utils.BytesToHexString(_data, " ")}])";
    }
}
