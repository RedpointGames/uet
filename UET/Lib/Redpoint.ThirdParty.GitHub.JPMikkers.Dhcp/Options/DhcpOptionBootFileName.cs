
using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionBootFileName : DhcpOptionBase
{
    private string _name = string.Empty;

    #region IDHCPOption Members

    public string Name
    {
        get
        {
            return _name;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionBootFileName result = new DhcpOptionBootFileName();
        result._name = ParseHelper.ReadString(s);
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteString(s, ZeroTerminatedStrings, _name);
    }

    #endregion

    public DhcpOptionBootFileName()
        : base(DhcpOptionType.BootFileName)
    {
    }

    public DhcpOptionBootFileName(string name)
        : base(DhcpOptionType.BootFileName)
    {
        _name = name;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_name}])";
    }
}
