using System.IO;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpOptionRelayAgentInformation : DhcpOptionBase
{
    // suboptions found here: http://networksorcery.com/enp/protocol/bootp/option082.htm
    private enum SubOption : byte
    {
        AgentCircuitId = 1,                         // RFC 3046
        AgentRemoteId = 2,                          // RFC 3046
        DOCSISDeviceClass = 4,                      // RFC 3256
        LinkSelection = 5,                          // RFC 3527
        SubscriberId = 6,                           // RFC 3993
        RadiusAttributes = 7,                       // RFC 4014
        Authentication = 8,                         // RFC 4030
        VendorSpecificInformation = 9,              // RFC 4243
        RelayAgentFlags = 10,                       // RFC 5010
        ServerIdentifierOverride = 11,              // RFC 5107
        DHCPv4VirtualSubnetSelection = 151,         // RFC 6607
        DHCPv4VirtualSubnetSelectionControl = 152,  // RFC 6607
    }

    private byte[] _data;
    private byte[] _agentCircuitId;
    private byte[] _agentRemoteId;

    public byte[] AgentCircuitId
    {
        get { return _agentCircuitId; }
    }

    public byte[] AgentRemoteId
    {
        get { return _agentRemoteId; }
    }

    #region IDHCPOption Members

    public override IDhcpOption FromStream(Stream s)
    {
        var result = new DhcpOptionRelayAgentInformation();
        result._data = new byte[s.Length];
        s.ReadExactly(result._data);

        // subOptionStream
        var suStream = new MemoryStream(_data);

        while(true)
        {
            int suCode = suStream.ReadByte();
            if(suCode == -1 || suCode == 255) break;
            else if(suCode == 0) continue;
            else
            {
                int suLen = suStream.ReadByte();
                if(suLen == -1) break;

                switch((SubOption)suCode)
                {
                    case SubOption.AgentCircuitId:
                        result._agentCircuitId = new byte[suLen];
                        suStream.Read(result._agentCircuitId, 0, suLen);
                        break;

                    case SubOption.AgentRemoteId:
                        result._agentRemoteId = new byte[suLen];
                        suStream.Read(result._agentRemoteId, 0, suLen);
                        break;

                    default:
                        suStream.Seek(suLen, SeekOrigin.Current);
                        break;
                }
            }
        }
        return result;
    }

    public override void ToStream(Stream s)
    {
        s.Write(_data, 0, _data.Length);
    }

    #endregion

    public DhcpOptionRelayAgentInformation()
        : base(DhcpOptionType.RelayAgentInformation)
    {
        _data = [];
        _agentCircuitId = [];
        _agentRemoteId = [];
    }

    public override string ToString()
    {
        return $"Option(name=[{OptionType}], value=[AgentCircuitId=[{Utils.BytesToHexString(_agentCircuitId, " ")}], AgentRemoteId=[{Utils.BytesToHexString(_agentCircuitId, " ")}]])";
    }
}
