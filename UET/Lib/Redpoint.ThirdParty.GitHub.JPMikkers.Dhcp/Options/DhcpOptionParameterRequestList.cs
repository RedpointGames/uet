using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionParameterRequestList : DhcpOptionBase
{
    private readonly List<DhcpOptionType> _requestList = new List<DhcpOptionType>();

    #region IDHCPOption Members

    public List<DhcpOptionType> RequestList
    {
        get
        {
            return _requestList;
        }
    }

    public override IDhcpOption FromStream(Stream s)
    {
        DhcpOptionParameterRequestList result = new DhcpOptionParameterRequestList();
        while(true)
        {
            int c = s.ReadByte();
            if(c < 0) break;
            result._requestList.Add((DhcpOptionType)c);
        }
        return result;
    }

    public override void ToStream(Stream s)
    {
        foreach(DhcpOptionType opt in _requestList)
        {
            s.WriteByte((byte)opt);
        }
    }

    #endregion

    public DhcpOptionParameterRequestList()
        : base(DhcpOptionType.ParameterRequestList)
    {
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        foreach(DhcpOptionType opt in _requestList)
        {
            sb.Append(opt.ToString());
            sb.Append(',');
        }
        if(_requestList.Count > 0) sb.Remove(sb.Length - 1, 1);
        return $"Option(name=[{OptionType}],value=[{sb}])";
    }
}
