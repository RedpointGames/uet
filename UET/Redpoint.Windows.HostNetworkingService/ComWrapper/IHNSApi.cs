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
    internal partial interface IHNSApi
    {
        public const string IID = "0B43D888-341A-4D8C-8C3A-F9EF5045DF01";

        #region IDispatch methods (unused, but necessary for vtable)

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

        #endregion

        [DispId(0x60020000)]
        void Request([MarshalAs(UnmanagedType.LPWStr)] string Method, [MarshalAs(UnmanagedType.LPWStr)] string Path, [MarshalAs(UnmanagedType.LPWStr)] string Object, [MarshalAs(UnmanagedType.LPWStr)] out string Response);

        [DispId(0x60020001)]
        void AttachEndpoint(ref Guid EndpointId, uint CompartmentId);

        [DispId(0x60020002)]
        void DetachEndpoint(ref Guid EndpointId);

        [DispId(0x60020003)]
        void AttachEndpointVNic(ref Guid EndpointId, [MarshalAs(UnmanagedType.LPWStr)] string NicName);

        [DispId(0x60020004)]
        void GetEndpointConfiguration(ref Guid EndpointId, out uint ConfigurationDataLength, out byte EndpointConfigurationData);

        [DispId(0x60020005)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string Request2([MarshalAs(UnmanagedType.BStr)] string Method, [MarshalAs(UnmanagedType.BStr)] string Path, [MarshalAs(UnmanagedType.BStr)] string Object);
    }
}