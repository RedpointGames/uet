using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace GitHub.JPMikkers.Dhcp;

public class DhcpMessage
{
    private static readonly IDhcpOption[] s_optionsTemplates;

    public enum TOpcode
    {
        Unknown = 0,
        BootRequest = 1,
        BootReply = 2
    }

    public enum THardwareType
    {
        Unknown = 0,
        Ethernet = 1,
        Experimental_Ethernet = 2,
        Amateur_Radio_AX_25 = 3,
        Proteon_ProNET_Token_Ring = 4,
        Chaos = 5,
        IEEE_802_Networks = 6,
        ARCNET = 7,
        Hyperchannel = 8,
        Lanstar = 9,
        Autonet_Short_Address = 10,
        LocalTalk = 11,
        LocalNet = 12,
        Ultra_link = 13,
        SMDS = 14,
        Frame_Relay = 15,
        Asynchronous_Transmission_Mode1 = 16,
        HDLC = 17,
        Fibre_Channel = 18,
        Asynchronous_Transmission_Mode2 = 19,
        Serial_Line = 20,
        Asynchronous_Transmission_Mode3 = 21,
    };

    private TOpcode _opcode;
    private THardwareType _hardwareType;
    private byte _hops;
    private uint _xID;
    private ushort _secs;
    private bool _broadCast;
    private IPAddress _clientIPAddress;
    private IPAddress _yourIPAddress;
    private IPAddress _nextServerIPAddress;
    private IPAddress _relayAgentIPAddress;
    private byte[] _clientHardwareAddress;
    private string _serverHostName;
    private string _bootFileName;
    private List<IDhcpOption> _options;

    /// <summary>
    /// op, Message op code / message type. 1 = BOOTREQUEST, 2 = BOOTREPLY
    /// </summary>
    public TOpcode Opcode
    {
        get { return _opcode; }
        set { _opcode = value; }
    }

    /// <summary>
    /// htype, Hardware address type, see ARP section in "Assigned Numbers" RFC; e.g., '1' = 10mb ethernet.
    /// </summary>
    public THardwareType HardwareType
    {
        get { return _hardwareType; }
        set { _hardwareType = value; }
    }

    /// <summary>
    /// Client sets to zero, optionally used by relay agents when booting via a relay agent.
    /// </summary>
    public byte Hops
    {
        get { return _hops; }
        set { _hops = value; }
    }

    /// <summary>
    /// xid (Transaction ID), a random number chosen by the client, used by the client and server 
    /// to associate messages and responses between a client and a server.
    /// </summary>
    public uint XID
    {
        get { return _xID; }
        set { _xID = value; }
    }

    /// <summary>
    /// Filled in by client, seconds elapsed since client began address acquisition or renewal process.
    /// </summary>
    public ushort Secs
    {
        get { return _secs; }
        set { _secs = value; }
    }

    /// <summary>
    /// Broadcast flag. From rfc2131: 
    /// If the 'giaddr' field in a DHCP message from a client is non-zero,
    /// the server sends any return messages to the 'DHCP server' port on the
    /// BOOTP relay agent whose address appears in 'giaddr'. If the 'giaddr'
    /// field is zero and the 'ciaddr' field is nonzero, then the server
    /// unicasts DHCPOFFER and DHCPACK messages to the address in 'ciaddr'.
    /// If 'giaddr' is zero and 'ciaddr' is zero, and the broadcast bit is
    /// set, then the server broadcasts DHCPOFFER and DHCPACK messages to
    /// 0xffffffff. If the broadcast bit is not set and 'giaddr' is zero and
    /// 'ciaddr' is zero, then the server unicasts DHCPOFFER and DHCPACK
    /// messages to the client's hardware address and 'yiaddr' address.  In
    /// all cases, when 'giaddr' is zero, the server broadcasts any DHCPNAK
    /// messages to 0xffffffff.
    /// </summary>
    public bool BroadCast
    {
        get { return _broadCast; }
        set { _broadCast = value; }
    }

    /// <summary>
    /// ciaddr (Client IP address) only filled in if client is in BOUND, RENEW or REBINDING state and can respond to ARP requests.
    /// </summary>
    public IPAddress ClientIPAddress
    {
        get { return _clientIPAddress; }
        set { _clientIPAddress = value; }
    }

    /// <summary>
    /// yiaddr, 'your' (client) IP address.
    /// </summary>
    public IPAddress YourIPAddress
    {
        get { return _yourIPAddress; }
        set { _yourIPAddress = value; }
    }

    /// <summary>
    /// siaddr, IP address of next server to use in bootstrap returned in DHCPOFFER, DHCPACK by server.
    /// </summary>
    public IPAddress NextServerIPAddress
    {
        get { return _nextServerIPAddress; }
        set { _nextServerIPAddress = value; }
    }

    /// <summary>
    /// giaddr (Relay agent IP address) used in booting via a relay agent.
    /// </summary>
    public IPAddress RelayAgentIPAddress
    {
        get { return _relayAgentIPAddress; }
        set { _relayAgentIPAddress = value; }
    }

    /// <summary>
    /// chaddr, Client hardware address.
    /// </summary>
    public byte[] ClientHardwareAddress
    {
        get { return _clientHardwareAddress; }
        set { _clientHardwareAddress = value; }
    }

    /// <summary>
    /// Optional server host name, null terminated string.
    /// </summary>
    public string ServerHostName
    {
        get { return _serverHostName; }
        set { _serverHostName = value; }
    }

    /// <summary>
    /// file (Boot file name) null terminated string; "generic" name or null in 
    /// DHCPDISCOVER, fully qualified directory-path name in DHCPOFFER.
    /// </summary>
    public string BootFileName
    {
        get { return _bootFileName; }
        set { _bootFileName = value; }
    }

    /// <summary>
    /// Optional parameters field.
    /// </summary>
    public List<IDhcpOption> Options
    {
        get { return _options; }
    }

    /// <summary>
    /// Convenience property to easily get or set the messagetype option
    /// </summary>
    public DhcpOptionMessageTypeType MessageType
    {
        get
        {
            var messageTypeDHCPOption = FindOption<DhcpOptionMessageType>();
            if (messageTypeDHCPOption != null)
            {
                return messageTypeDHCPOption.MessageType;
            }
            else
            {
                return DhcpOptionMessageTypeType.Undefined;
            }
        }
        set
        {
            DhcpOptionMessageTypeType currentMessageType = MessageType;
            if (currentMessageType != value)
            {
                _options.Add(new DhcpOptionMessageType(value));
            }
        }
    }

    private static void RegisterOption(IDhcpOption option)
    {
        s_optionsTemplates[(int)option.OptionType] = option;
    }

    static DhcpMessage()
    {
        s_optionsTemplates = new IDhcpOption[256];
        for (int t = 1; t < 255; t++)
        {
            s_optionsTemplates[t] = new DhcpOptionGeneric((DhcpOptionType)t);
        }

        RegisterOption(new DhcpOptionFixedLength(DhcpOptionType.Pad));
        RegisterOption(new DhcpOptionFixedLength(DhcpOptionType.End));
        RegisterOption(new DhcpOptionHostName());
        RegisterOption(new DhcpOptionIPAddressLeaseTime());
        RegisterOption(new DhcpOptionServerIdentifier());
        RegisterOption(new DhcpOptionRequestedIPAddress());
        RegisterOption(new DhcpOptionOptionOverload());
        RegisterOption(new DhcpOptionTftpServerName());
        RegisterOption(new DhcpOptionBootFileName());
        RegisterOption(new DhcpOptionMessageType());
        RegisterOption(new DhcpOptionMessage());
        RegisterOption(new DhcpOptionMaximumDhcpMessageSize());
        RegisterOption(new DhcpOptionParameterRequestList());
        RegisterOption(new DhcpOptionRenewalTimeValue());
        RegisterOption(new DhcpOptionRebindingTimeValue());
        RegisterOption(new DhcpOptionVendorClassIdentifier());
        RegisterOption(new DhcpOptionClientIdentifier());
        RegisterOption(new DhcpOptionFullyQualifiedDomainName());
        RegisterOption(new DhcpOptionSubnetMask());
        RegisterOption(new DhcpOptionRouter());
        RegisterOption(new DhcpOptionDomainNameServer());
        RegisterOption(new DhcpOptionNetworkTimeProtocolServers());
#if RELAYAGENTINFORMATION
        RegisterOption(new DHCPOptionRelayAgentInformation());
#endif
    }

    public DhcpMessage()
    {
        _hardwareType = THardwareType.Ethernet;
        _clientIPAddress = IPAddress.Any;
        _yourIPAddress = IPAddress.Any;
        _nextServerIPAddress = IPAddress.Any;
        _relayAgentIPAddress = IPAddress.Any;
        _clientHardwareAddress = new byte[0];
        _serverHostName = "";
        _bootFileName = "";
        _options = new List<IDhcpOption>();
    }

    public T? FindOption<T>() where T : DhcpOptionBase
    {
        return _options.OfType<T>().FirstOrDefault();
    }

    public IDhcpOption? GetOption(DhcpOptionType optionType)
    {
        return _options.Find(delegate (IDhcpOption v) { return v.OptionType == optionType; });
    }

    public bool IsRequestedParameter(DhcpOptionType optionType)
    {
        var dhcpOptionParameterRequestList = FindOption<DhcpOptionParameterRequestList>();
        return (dhcpOptionParameterRequestList != null && dhcpOptionParameterRequestList.RequestList.Contains(optionType));
    }

    private DhcpMessage(Stream s) : this()
    {
        _opcode = (TOpcode)s.ReadByte();
        _hardwareType = (THardwareType)s.ReadByte();
        _clientHardwareAddress = new byte[s.ReadByte()];
        _hops = (byte)s.ReadByte();
        _xID = ParseHelper.ReadUInt32(s);
        _secs = ParseHelper.ReadUInt16(s);
        _broadCast = ((ParseHelper.ReadUInt16(s) & 0x8000) == 0x8000);
        _clientIPAddress = ParseHelper.ReadIPAddress(s);
        _yourIPAddress = ParseHelper.ReadIPAddress(s);
        _nextServerIPAddress = ParseHelper.ReadIPAddress(s);
        _relayAgentIPAddress = ParseHelper.ReadIPAddress(s);
        s.ReadExactly(_clientHardwareAddress);
        for (int t = _clientHardwareAddress.Length; t < 16; t++) s.ReadByte();

        byte[] serverHostNameBuffer = new byte[64];
        s.ReadExactly(serverHostNameBuffer);

        byte[] bootFileNameBuffer = new byte[128];
        s.ReadExactly(bootFileNameBuffer);

        // read options magic cookie
        if (s.ReadByte() != 99) throw new IOException();
        if (s.ReadByte() != 130) throw new IOException();
        if (s.ReadByte() != 83) throw new IOException();
        if (s.ReadByte() != 99) throw new IOException();

        byte[] optionsBuffer = new byte[s.Length - s.Position];
        s.ReadExactly(optionsBuffer);

        byte overload = ScanOverload(new MemoryStream(optionsBuffer));

        switch (overload)
        {
            default:
                _serverHostName = ParseHelper.ReadZString(new MemoryStream(serverHostNameBuffer));
                _bootFileName = ParseHelper.ReadZString(new MemoryStream(bootFileNameBuffer));
                _options = ReadOptions(optionsBuffer, new byte[0], new byte[0]);
                break;

            case 1:
                _serverHostName = ParseHelper.ReadZString(new MemoryStream(serverHostNameBuffer));
                _options = ReadOptions(optionsBuffer, bootFileNameBuffer, new byte[0]);
                break;

            case 2:
                _bootFileName = ParseHelper.ReadZString(new MemoryStream(bootFileNameBuffer));
                _options = ReadOptions(optionsBuffer, serverHostNameBuffer, new byte[0]);
                break;

            case 3:
                _options = ReadOptions(optionsBuffer, bootFileNameBuffer, serverHostNameBuffer);
                break;
        }
    }

    private static List<IDhcpOption> ReadOptions(byte[] buffer1, byte[] buffer2, byte[] buffer3)
    {
        var result = new List<IDhcpOption>();
        ReadOptions(result, new MemoryStream(buffer1, true), new MemoryStream(buffer2, true), new MemoryStream(buffer3, true));
        ReadOptions(result, new MemoryStream(buffer2, true), new MemoryStream(buffer3, true));
        ReadOptions(result, new MemoryStream(buffer3, true));
        return result;
    }

    private static void CopyBytes(Stream source, Stream target, int length)
    {
        byte[] buffer = new byte[length];
        source.ReadExactly(buffer, 0, length);
        target.Write(buffer, 0, length);
    }

    private static void ReadOptions(List<IDhcpOption> options, MemoryStream s, params MemoryStream[] spillovers)
    {
        while (true)
        {
            int code = s.ReadByte();
            if (code == -1 || code == 255) break;
            else if (code == 0) continue;
            else
            {
                MemoryStream concatStream = new MemoryStream();
                int len = s.ReadByte();
                if (len == -1) break;
                CopyBytes(s, concatStream, len);
                AppendOverflow(code, s, concatStream);
                foreach (MemoryStream spillOver in spillovers)
                {
                    AppendOverflow(code, spillOver, concatStream);
                }
                concatStream.Position = 0;
                options.Add(s_optionsTemplates[code].FromStream(concatStream));
            }
        }
    }

    private static void AppendOverflow(int code, MemoryStream source, MemoryStream target)
    {
        long initPosition = source.Position;
        try
        {
            while (true)
            {
                int c = source.ReadByte();
                if (c == -1 || c == 255) break;
                else if (c == 0) continue;
                else
                {
                    int l = source.ReadByte();
                    if (l == -1) break;

                    if (c == code)
                    {
                        long startPosition = source.Position - 2;
                        CopyBytes(source, target, l);
                        source.Position = startPosition;
                        for (int t = 0; t < (l + 2); t++)
                        {
                            source.WriteByte(0);
                        }
                    }
                    else
                    {
                        source.Seek(l, SeekOrigin.Current);
                    }
                }
            }
        }
        finally
        {
            source.Position = initPosition;
        }
    }

    /// <summary>
    /// Locate the overload option value in the passed stream.
    /// </summary>
    /// <param name="s"></param>
    /// <returns>Returns the overload option value, or 0 if it wasn't found</returns>
    private static byte ScanOverload(Stream s)
    {
        byte result = 0;

        while (true)
        {
            int code = s.ReadByte();
            if (code == -1 || code == 255) break;
            else if (code == 0) continue;
            else if (code == 52)
            {
                if (s.ReadByte() != 1) throw new IOException("Invalid length of DHCP option 'Option Overload'");
                result = (byte)s.ReadByte();
            }
            else
            {
                int l = s.ReadByte();
                if (l == -1) break;
                s.Position += l;
            }
        }
        return result;
    }

    public static DhcpMessage FromStream(Stream s)
    {
        return new DhcpMessage(s);
    }

    public void ToStream(Stream s, int minimumPacketSize)
    {
        s.WriteByte((byte)_opcode);
        s.WriteByte((byte)_hardwareType);
        s.WriteByte((byte)_clientHardwareAddress.Length);
        s.WriteByte((byte)_hops);
        ParseHelper.WriteUInt32(s, _xID);
        ParseHelper.WriteUInt16(s, _secs);
        ParseHelper.WriteUInt16(s, _broadCast ? (ushort)0x8000 : (ushort)0x0);
        ParseHelper.WriteIPAddress(s, _clientIPAddress);
        ParseHelper.WriteIPAddress(s, _yourIPAddress);
        ParseHelper.WriteIPAddress(s, _nextServerIPAddress);
        ParseHelper.WriteIPAddress(s, _relayAgentIPAddress);
        s.Write(_clientHardwareAddress, 0, _clientHardwareAddress.Length);
        for (int t = _clientHardwareAddress.Length; t < 16; t++) s.WriteByte(0);
        ParseHelper.WriteZString(s, _serverHostName, 64);  // BOOTP legacy
        ParseHelper.WriteZString(s, _bootFileName, 128);   // BOOTP legacy
        s.Write(new byte[] { 99, 130, 83, 99 }, 0, 4);  // options magic cookie

        // write all options except RelayAgentInformation
        foreach (var option in _options.Where(x => x.OptionType != DhcpOptionType.RelayAgentInformation))
        {
            var optionStream = new MemoryStream();
            option.ToStream(optionStream);
            s.WriteByte((byte)option.OptionType);
            s.WriteByte((byte)optionStream.Length);
            optionStream.Position = 0;
            CopyBytes(optionStream, s, (int)optionStream.Length);
        }

#if RELAYAGENTINFORMATION
        // RelayAgentInformation should be the last option before the end according to RFC 3046
        foreach (var option in _options.Where(x => x.OptionType == TDHCPOption.RelayAgentInformation))
        {
            var optionStream = new MemoryStream();
            option.ToStream(optionStream);
            s.WriteByte((byte)option.OptionType);
            s.WriteByte((byte)optionStream.Length);
            optionStream.Position = 0;
            CopyBytes(optionStream, s, (int)optionStream.Length);
        }
#endif
        // write end option
        s.WriteByte(255);
        s.Flush();

        while (s.Length < minimumPacketSize)
        {
            s.WriteByte(0);
        }

        s.Flush();
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendFormat("Opcode (op)                    : {0}\r\n", _opcode);
        sb.AppendFormat("HardwareType (htype)           : {0}\r\n", _hardwareType);
        sb.AppendFormat("Hops                           : {0}\r\n", _hops);
        sb.AppendFormat("XID                            : {0}\r\n", _xID);
        sb.AppendFormat("Secs                           : {0}\r\n", _secs);
        sb.AppendFormat("BroadCast (flags)              : {0}\r\n", _broadCast);
        sb.AppendFormat("ClientIPAddress (ciaddr)       : {0}\r\n", _clientIPAddress);
        sb.AppendFormat("YourIPAddress (yiaddr)         : {0}\r\n", _yourIPAddress);
        sb.AppendFormat("NextServerIPAddress (siaddr)   : {0}\r\n", _nextServerIPAddress);
        sb.AppendFormat("RelayAgentIPAddress (giaddr)   : {0}\r\n", _relayAgentIPAddress);
        sb.AppendFormat("ClientHardwareAddress (chaddr) : {0}\r\n", Utils.BytesToHexString(_clientHardwareAddress, "-"));
        sb.AppendFormat("ServerHostName (sname)         : {0}\r\n", _serverHostName);
        sb.AppendFormat("BootFileName (file)            : {0}\r\n", _bootFileName);

        foreach (IDhcpOption option in _options)
        {
            sb.AppendFormat("Option                         : {0}\r\n", option.ToString());
        }

        return sb.ToString();
    }
}
