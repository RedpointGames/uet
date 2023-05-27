namespace Redpoint.Vfs.Abstractions
{
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public ref struct AsyncIoNativeOverlapped
    {
        public nuint Status;
        private nint _internalHigh;
        public long Offset;
        public nint EventHandle;
    }

    [SupportedOSPlatform("windows6.2")]
    public interface IAsyncIoProcessing
    {
        unsafe AsyncIoNativeOverlapped* AllocateNativeOverlapped(IAsyncIoHandle ioHandle, ulong requestHint, VfsFileAsyncCallback callback);

        unsafe void CancelNativeOverlapped(nint overlapped);
    }
}
