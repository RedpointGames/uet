namespace Redpoint.Vfs.Abstractions
{
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    /// <summary>
    /// Represents the NATIVEOVERLAPPED structure in the Win32 API.
    /// </summary>
    [SupportedOSPlatform("windows6.2")]
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public ref struct AsyncIoNativeOverlapped
    {
#pragma warning disable CS1591
        public nuint Status;
        private nint _internalHigh;
        public long Offset;
        public nint EventHandle;
#pragma warning restore CS1591
    }
}
