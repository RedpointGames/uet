namespace Redpoint.AutoDiscovery.Windows
{
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct DNS_SERVICE_INSTANCE
    {
        public unsafe char* InstanceName;
        public unsafe char* HostName;
        public unsafe uint* Ipv4Address;
        public unsafe fixed byte Ipv6Address[16];
        public ushort Port;
        public ushort Priority;
        public ushort Weight;
        public uint PropertyCount;
        public unsafe char* Keys;
        public unsafe char* Values;
        public uint InterfaceIndex;
    }
}
