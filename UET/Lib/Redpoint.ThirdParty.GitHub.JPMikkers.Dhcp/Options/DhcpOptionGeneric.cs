using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionGeneric : DhcpOptionBase
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
        DhcpOptionGeneric result = new DhcpOptionGeneric(_optionType);
        result._data = new byte[s.Length];
        s.ReadExactly(result._data);
        return result;
    }

    public override void ToStream(Stream s)
    {
        s.Write(_data, 0, _data.Length);
    }

    #endregion

    public DhcpOptionGeneric(DhcpOptionType option) : base(option)
    {
        _data = [];
    }

    public DhcpOptionGeneric(DhcpOptionType option, byte[] data) : base(option)
    {
        _data = data;
    }

    public override string ToString()
    {
        return $"Option(name=[{_optionType}],value=[{Utils.BytesToHexString(_data, " ")}])";
    }
}
