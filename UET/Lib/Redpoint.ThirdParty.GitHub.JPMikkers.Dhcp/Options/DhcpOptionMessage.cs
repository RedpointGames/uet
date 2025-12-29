using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionMessage : DhcpOptionBase
{
    private string _message = string.Empty;

    #region IDHCPOption Members

    public string Message
    {
        get
        {
            return _message;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionMessage result = new DhcpOptionMessage();
        result._message = ParseHelper.ReadString(s);
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteString(s, ZeroTerminatedStrings, _message);
    }

    #endregion

    public DhcpOptionMessage()
        : base(DhcpOptionType.Message)
    {
    }

    public DhcpOptionMessage(string message)
        : base(DhcpOptionType.Message)
    {
        _message = message;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_message}])";
    }
}
