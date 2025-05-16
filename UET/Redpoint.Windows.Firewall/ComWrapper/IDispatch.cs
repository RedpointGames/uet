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
    internal partial interface IDispatch
    {
        public const string IID = "9C4C6277-5027-441E-AFAE-CA1F542DA010";

        [PreserveSig]
        int GetTypeInfoCount(out uint pctinfo);

        [PreserveSig]
        int GetTypeInfo(
            uint itinfo,
            ulong lcid,
            out nint pptinfo);

        [PreserveSig]
        int GetIDsOfNames(
            ref Guid riid,
            nint rgszNames,
            uint cNames,
            ulong lcid,
            out long rgdispid);

        [PreserveSig]
        int Invoke(
            long dispidMember,
            ref Guid riid,
            ulong lcid,
            ushort wFlags,
            nint pdispparams,
            nint pvarResult,
            nint pexcepinfo,
            nint puArgErr);
    }
}