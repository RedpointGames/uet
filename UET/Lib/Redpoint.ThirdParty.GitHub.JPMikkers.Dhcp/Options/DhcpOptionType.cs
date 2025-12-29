namespace GitHub.JPMikkers.Dhcp;

public enum DhcpOptionType
{
    // 3: RFC 1497 Vendor Extensions
    Pad = 0,
    SubnetMask = 1,
    TimeOffset = 2,
    Router = 3,
    TimeServer = 4,
    NameServer = 5,
    DomainNameServer = 6,
    LogServer = 7,
    CookieServer = 8,
    LPRServer = 9,
    ImpressServer = 10,
    ResourceLocationServer = 11,
    HostName = 12,
    BootFileSize = 13,
    MeritDumpFile = 14,
    DomainName = 15,
    SwapServer = 16,
    RootPath = 17,
    ExtensionPath = 18,

    // 4: IP Layer Parameters per Host
    IPForwardingEnable = 19,
    NonLocalSourceRoutingEnable = 20,
    PolicyFilter = 21,
    MaximumDatagramReassembly = 22,
    DefaultIPTTL = 23,
    PathMTUAgingTimeout = 24,
    PathMTUPlateauTable = 25,

    // 5: IP Layer Parameters per Interface
    InterfaceMTU = 26,
    AllSubnetsAreLocal = 27,
    BroadcastAddress = 28,
    PerformMaskDiscovery = 29,
    MaskSupplier = 30,
    PerformRouterDiscovery = 31,
    RouterSolicitationAddress = 32,
    StaticRoute = 33,

    // 6: Link Layer Parameters per Interface
    TrailerEncapsulation = 34,
    ARPCacheTimeout = 35,
    EthernetEncapsulation = 36,

    // 7: TCP Parameters
    TCPDefaultTTL = 37,
    TCPKeepaliveInterval = 38,
    TCPKeepaliveGarbage = 39,

    // 8: Application and Service parameters
    NetworkInformationServiceDomain = 40,
    NetworkInformationServiceServers = 41,
    NetworkTimeProtocolServers = 42,
    VendorSpecificInformation = 43,
    NetBIOSOverTCPIPNameServer = 44,
    NetBIOSOverTCPIPDatagramDistributionServer = 45,
    NetBIOSOverTCPIPNodeType = 46,
    NetBIOSOverTCPIPScope = 47,
    XWindowSystemFontServer = 48,
    XWindowSystemDisplayManager = 49,
    NetworkInformationServicePlusDomain = 64,
    NetworkInformationServicePlusServers = 65,
    MobileIPHomeAgent = 68,
    SimpleMailTransportProtocolServer = 69,
    PostOfficeProtocolServer = 70,
    NetworkNewsTransportProtocolServer = 71,
    DefaultWorldWideWebServer = 72,
    DefaultFingerServer = 73,
    DefaultInternetRelayChat = 74,
    StreetTalkServer = 75,
    StreetTalkDirectoryAssistanceServer = 76,

    // 9: DHCP Extensions
    RequestedIPAddress = 50,    // this option is used in a client request to allow the client to request a particular IP address to be assigned
    IPAddressLeaseTime = 51,
    OptionOverload = 52,
    MessageType = 53,
    ServerIdentifier = 54,
    ParameterRequestList = 55,
    Message = 56,
    MaximumDHCPMessageSize = 57,
    RenewalTimeValue = 58,
    RebindingTimeValue = 59,
    VendorClassIdentifier = 60,
    ClientIdentifier = 61,
    TFTPServerName = 66,
    BootFileName = 67,

    FullyQualifiedDomainName = 81,              // RFC4702
    RelayAgentInformation = 82,                 // RFC3046, RFC6607

    ClientSystemArchitectureType = 93,          // RFC4578
    ClientNetworkInterfaceIdentifier = 94,      // RFC4578
    ClientMachineIdentifier = 97,               // RFC4578

    AutoConfigure = 116,                        // RFC2563
    ClasslessStaticRoutesA = 121,               // RFC3442

    /*
        128   TFPT Server IP address                        // RFC 4578 
        129   Call Server IP address                        // RFC 4578 
        130   Discrimination string                         // RFC 4578 
        131   Remote statistics server IP address           // RFC 4578 
        132   802.1P VLAN ID
        133   802.1Q L2 Priority
        134   Diffserv Code Point
        135   HTTP Proxy for phone-specific applications    
     */

    ClasslessStaticRoutesB = 249,

    End = 255,
}
