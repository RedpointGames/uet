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
    internal partial interface INetFwRules : IDispatch
    {
        public new const string IID = "9C4C6277-5027-441E-AFAE-CA1F542DA009";

        long Count();

        void Add(INetFwRule Rule);

        void Remove([MarshalAs(UnmanagedType.BStr)] string Name);

        INetFwRule? Item([MarshalAs(UnmanagedType.BStr)] string Name);
    }
}