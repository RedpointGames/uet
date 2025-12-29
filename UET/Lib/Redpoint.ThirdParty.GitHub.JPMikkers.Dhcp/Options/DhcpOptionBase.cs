using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public abstract class DhcpOptionBase : IDhcpOption
{
    protected DhcpOptionType _optionType;

    public DhcpOptionType OptionType
    {
        get
        {
            return _optionType;
        }
    }

    public bool ZeroTerminatedStrings { get; set; }

    public abstract IDhcpOption FromStream(Stream s);
    public abstract void ToStream(Stream s);

    protected DhcpOptionBase(DhcpOptionType optionType)
    {
        _optionType = optionType;
    }
}
