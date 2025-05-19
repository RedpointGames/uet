﻿namespace Redpoint.Windows.HostNetworkingService.ComWrapper
{
    using System.Runtime.InteropServices.Marshalling;
    using System.Runtime.InteropServices;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    internal static unsafe partial class Ole32
    {
        // https://docs.microsoft.com/windows/win32/api/combaseapi/nf-combaseapi-cocreateinstance
        [LibraryImport(nameof(Ole32))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static partial int CoCreateInstance(
            ref Guid rclsid,
            nint pUnkOuter,
            uint dwClsContext,
            ref Guid riid,
            // The default ComInterfaceMarshaller will unwrap a .NET object if it can tell the COM instance is a ComWrapper.
            // This causes issues when casting to ICalculator, since the Server's Calculator class doesn't implement the Client's interface.
            // UniqueComInterfaceMarshaller doesn't try to unwrap the object and always creates a new COM object.
            [MarshalUsing(typeof(UniqueComInterfaceMarshaller<object>))]
            out object ppv);

        // https://learn.microsoft.com/windows/win32/api/wtypesbase/ne-wtypesbase-clsctx
        [SuppressMessage("Naming", "CA1712:Do not prefix enum values with type name", Justification = "Enumeration value names match the C++ definitions.")]
        public enum CLSCTX : uint
        {
            CLSCTX_INPROC_SERVER = 0x1,
            CLSCTX_INPROC_HANDLER = 0x2,
            CLSCTX_LOCAL_SERVER = 0x4,
            CLSCTX_INPROC_SERVER16 = 0x8,
            CLSCTX_REMOTE_SERVER = 0x10,
            CLSCTX_INPROC_HANDLER16 = 0x20,
            CLSCTX_RESERVED1 = 0x40,
            CLSCTX_RESERVED2 = 0x80,
            CLSCTX_RESERVED3 = 0x100,
            CLSCTX_RESERVED4 = 0x200,
            CLSCTX_NO_CODE_DOWNLOAD = 0x400,
            CLSCTX_RESERVED5 = 0x800,
            CLSCTX_NO_CUSTOM_MARSHAL = 0x1000,
            CLSCTX_ENABLE_CODE_DOWNLOAD = 0x2000,
            CLSCTX_NO_FAILURE_LOG = 0x4000,
            CLSCTX_DISABLE_AAA = 0x8000,
            CLSCTX_ENABLE_AAA = 0x10000,
            CLSCTX_FROM_DEFAULT_CONTEXT = 0x20000,
            CLSCTX_ACTIVATE_X86_SERVER = 0x40000,
            CLSCTX_ACTIVATE_32_BIT_SERVER,
            CLSCTX_ACTIVATE_64_BIT_SERVER = 0x80000,
            CLSCTX_ENABLE_CLOAKING = 0x100000,
            CLSCTX_APPCONTAINER = 0x400000,
            CLSCTX_ACTIVATE_AAA_AS_IU = 0x800000,
            CLSCTX_RESERVED6 = 0x1000000,
            CLSCTX_ACTIVATE_ARM32_SERVER = 0x2000000,
            CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION,
            CLSCTX_PS_DLL = 0x80000000,
        }
    }
}