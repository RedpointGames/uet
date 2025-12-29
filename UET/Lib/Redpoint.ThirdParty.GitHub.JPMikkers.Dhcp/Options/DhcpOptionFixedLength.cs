using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionFixedLength : DhcpOptionBase
{
    #region IDHCPOption Members

    public override IDhcpOption FromStream(Stream s)
    {
        return this;
    }

    public override void ToStream(Stream s)
    {
    }

    #endregion

    public DhcpOptionFixedLength(DhcpOptionType option) : base(option)
    {
    }

    public override string ToString()
    {
        return $"Option(name=[{_optionType}],value=[])";
    }
}
