using System;
using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionIPAddressLeaseTime : DhcpOptionBase
{
    private TimeSpan _leaseTime;

    #region IDHCPOption Members

    public TimeSpan LeaseTime
    {
        get
        {
            return _leaseTime;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionIPAddressLeaseTime result = new DhcpOptionIPAddressLeaseTime();
        if(s.Length != 4) throw new IOException("Invalid DHCP option length");
        result._leaseTime = TimeSpan.FromSeconds(ParseHelper.ReadUInt32(s));
        return result;
    }

    public override void ToStream(Stream s)
    {
        ParseHelper.WriteUInt32(s, (uint)_leaseTime.TotalSeconds);
    }

    #endregion

    public DhcpOptionIPAddressLeaseTime()
        : base(DhcpOptionType.IPAddressLeaseTime)
    {
    }

    public DhcpOptionIPAddressLeaseTime(TimeSpan leaseTime)
        : base(DhcpOptionType.IPAddressLeaseTime)
    {
        _leaseTime = leaseTime;
        if(_leaseTime > Utils.InfiniteTimeSpan)
        {
            _leaseTime = Utils.InfiniteTimeSpan;
        }
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}],value=[{(_leaseTime == Utils.InfiniteTimeSpan ? "Infinite" : _leaseTime.ToString())}])";
    }
}
