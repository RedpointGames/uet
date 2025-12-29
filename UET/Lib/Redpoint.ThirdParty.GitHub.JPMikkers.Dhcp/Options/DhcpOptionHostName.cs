using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionHostName : DhcpOptionBase
{
    private string _hostName = string.Empty;

    #region IDHCPOption Members

    public string HostName
    {
        get
        {
            return _hostName;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionHostName result = new DhcpOptionHostName();
        result._hostName = ParseHelper.ReadString(s);
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteString(s, ZeroTerminatedStrings, _hostName);
    }

    #endregion

    public DhcpOptionHostName()
        : base(DhcpOptionType.HostName)
    {
    }

    public DhcpOptionHostName(string hostName)
        : base(DhcpOptionType.HostName)
    {
        _hostName = hostName;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_hostName}])";
    }
}
