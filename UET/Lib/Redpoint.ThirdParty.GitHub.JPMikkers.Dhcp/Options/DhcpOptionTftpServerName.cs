using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionTftpServerName : DhcpOptionBase
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
        DhcpOptionTftpServerName result = new DhcpOptionTftpServerName();
        result._name = ParseHelper.ReadString(s);
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteString(s, ZeroTerminatedStrings, _name);
    }

    #endregion

    public DhcpOptionTftpServerName()
        : base(DhcpOptionType.TFTPServerName)
    {
    }

    public DhcpOptionTftpServerName(string name)
        : base(DhcpOptionType.TFTPServerName)
    {
        _name = name;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_name}])";
    }
}
