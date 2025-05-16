namespace Redpoint.Windows.HostNetworkingService.ComWrapper
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices.Marshalling;
    using System.Runtime.InteropServices;
    using Redpoint.Windows.HostNetworkingService;
    using System.Runtime.Versioning;
    using System;

    [GeneratedComInterface]
    [Guid(IID)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SupportedOSPlatform("windows")]
    internal partial interface INetFwRule : IDispatch
    {
        public new const string IID = "AF230D27-BABA-4E42-ACED-F524F22CFCE2";

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetName();

        void SetName([MarshalAs(UnmanagedType.BStr)] string Name);

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetDescription();

        void SetDescription([MarshalAs(UnmanagedType.BStr)] string Description);

        [return: MarshalAs(UnmanagedType.BStr)]
        string? GetApplicationName();

        void SetApplicationName([MarshalAs(UnmanagedType.BStr)] string? ApplicationName);

        [return: MarshalAs(UnmanagedType.BStr)]
        string? GetServiceName();

        void SetServiceName([MarshalAs(UnmanagedType.BStr)] string? ServiceName);

        NetFwIpProtocol GetProtocol();

        void SetProtocol(NetFwIpProtocol ServiceName);

        [return: MarshalAs(UnmanagedType.BStr)]
        string? GetLocalPorts();

        void SetLocalPorts([MarshalAs(UnmanagedType.BStr)] string? LocalPorts);

        [return: MarshalAs(UnmanagedType.BStr)]
        string? GetRemotePorts();

        void SetRemotePorts([MarshalAs(UnmanagedType.BStr)] string? RemotePorts);

        [return: MarshalAs(UnmanagedType.BStr)]
        string? GetLocalAddresses();

        void SetLocalAddresses([MarshalAs(UnmanagedType.BStr)] string? LocalAddresses);

        [return: MarshalAs(UnmanagedType.BStr)]
        string? GetRemoteAddresses();

        void SetRemoteAddresses([MarshalAs(UnmanagedType.BStr)] string? RemoteAddresses);

        [return: MarshalAs(UnmanagedType.BStr)]
        string? GetIcmpTypesAndCodes();

        void SetIcmpTypesAndCodes([MarshalAs(UnmanagedType.BStr)] string? IcmpTypesAndCodes);

        NetFwRuleDirection GetDirection();

        void SetDirection(NetFwRuleDirection IcmpTypesAndCodes);

        int GetInterfaces();

        void SetInterfaces(int Interfaces);

        [return: MarshalAs(UnmanagedType.BStr)]
        string? GetInterfaceTypes();

        void SetInterfaceTypes([MarshalAs(UnmanagedType.BStr)] string? InterfaceTypes);

        int GetEnabled();

        void SetEnabled(int Enabled);

        [return: MarshalAs(UnmanagedType.BStr)]
        string? GetGrouping();

        void SetGrouping([MarshalAs(UnmanagedType.BStr)] string? Grouping);

        long GetProfiles();

        void SetProfiles(long Profiles);

        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetEdgeTraversal();

        void SetEdgeTraversal([MarshalAs(UnmanagedType.Bool)] bool EdgeTraversal);

        NetFwAction GetAction();

        void SetAction(NetFwAction Action);
    }
}