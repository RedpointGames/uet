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
    internal partial interface INetFwPolicy : IDispatch
    {
        public new const string IID = "D46D2478-9AC9-4008-9DC7-5563CE5536CC";

        nint GetCurrentProfile();

        nint GetProfileByType(NetFwProfileType profileType);
    }
}