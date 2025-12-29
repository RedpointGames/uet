using System;
using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionRebindingTimeValue : DhcpOptionBase
{
    private TimeSpan _timeSpan;

    #region IDHCPOption Members

    public TimeSpan TimeSpan
    {
        get
        {
            return _timeSpan;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionRebindingTimeValue result = new DhcpOptionRebindingTimeValue();
        if(s.Length != 4) throw new IOException("Invalid DHCP option length");
        result._timeSpan = TimeSpan.FromSeconds(ParseHelper.ReadUInt32(s));
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteUInt32(s, (uint)_timeSpan.TotalSeconds);
    }

    #endregion

    public DhcpOptionRebindingTimeValue()
        : base(DhcpOptionType.RebindingTimeValue)
    {
    }

    public DhcpOptionRebindingTimeValue(TimeSpan timeSpan)
        : base(DhcpOptionType.RebindingTimeValue)
    {
        _timeSpan = timeSpan;
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{_timeSpan}])";
    }
}
