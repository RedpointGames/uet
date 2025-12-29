using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public interface IDhcpOption
{
    bool ZeroTerminatedStrings { get; set; }
    DhcpOptionType OptionType { get; }
    IDhcpOption FromStream(Stream s);
    void ToStream(Stream s);
}
