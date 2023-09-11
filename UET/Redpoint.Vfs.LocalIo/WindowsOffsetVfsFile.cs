namespace Redpoint.Vfs.LocalIo
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Windows;
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal sealed class WindowsOffsetVfsFile : IVfsFile, IVfsFileHandle<IVfsFile>, IAsyncIoHandle
    {
        private readonly ILogger _logger;
        private readonly SafeFileHandle _handle;
        private readonly long _offset;
        private readonly long _length;

        internal WindowsOffsetVfsFile(ILogger logger, string path, long offset, long length)
        {
            _handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            _offset = offset;
            _length = length;
            _logger = logger;
        }

        public IVfsFile VfsFile => this;

        public long Length => _length;

        public SafeFileHandle SafeFileHandle => _handle;

        public void Dispose()
        {
            _handle.Dispose();
        }

        public int ReadFile(byte[] buffer, out uint bytesRead, long offset)
        {
            GCHandle pinnedArray = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                return ReadFileUnsafe(
                    pinnedArray.AddrOfPinnedObject(),
                    (uint)buffer.Length,
                    out bytesRead,
                    offset);
            }
            finally
            {
                pinnedArray.Free();
            }
        }

        public int ReadFileUnsafe(IntPtr buffer, uint bufferLength, out uint bytesRead, long offset)
        {
            unsafe
            {
                long readLength = bufferLength;
                if (offset + readLength >= _length)
                {
                    readLength = _length - offset;
                }

                AsyncIoNativeOverlapped overlapped = new AsyncIoNativeOverlapped()
                {
                    Offset = _offset + offset,
                    EventHandle = IntPtr.Zero,
                };
                if (!NativeMethods.ReadFile(_handle, buffer, (uint)readLength, out bytesRead, (nint)(&overlapped)))
                {
                    bytesRead = 0;
                    var hresult = Marshal.GetHRForLastWin32Error();
                    if (hresult == HResultConstants.EOF)
                    {
                        // This is fine, it's a normal EOF.
                        return 0;
                    }
                    _logger.LogError($"NativeMethods.ReadFile: {offset} {hresult:X}");
                    return hresult;
                }
                return 0;
            }
        }

        public int ReadFileUnsafeAsync(IntPtr buffer, uint bufferLength, out uint bytesReadOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback)
        {
            unsafe
            {
                long readLength = bufferLength;
                if (offset + readLength >= _length)
                {
                    readLength = _length - offset;
                }

                bool release = true;
                AsyncIoNativeOverlapped* overlapped = asyncIo.AllocateNativeOverlapped(this, requestHint, callback);
                var ptr = (nint)overlapped;
                try
                {
                    overlapped->Offset = _offset + offset;
                    if (!NativeMethods.ReadFile(_handle, buffer, (uint)readLength, out bytesReadOnSyncResult, ptr))
                    {
                        bytesReadOnSyncResult = 0;
                        var hresult = Marshal.GetHRForLastWin32Error();
                        if (hresult == HResultConstants.IoPending)
                        {
                            // This is fine, the operation is pending.
                            release = false;
                            return hresult;
                        }
                        if (hresult != HResultConstants.EOF)
                        {
                            _logger.LogError($"NativeMethods.ReadFile: {offset} {hresult:X}");
                        }
                        return hresult;
                    }
                    return 0;
                }
                finally
                {
                    if (release)
                    {
                        asyncIo.CancelNativeOverlapped(ptr);
                    }
                }
            }
        }

        public int SetEndOfFile(long length)
        {
            return HResultConstants.AccessDenied;
        }

        public int WriteFile(byte[] buffer, uint bytesToWrite, out uint bytesWritten, long offset)
        {
            bytesWritten = 0;
            return HResultConstants.AccessDenied;
        }

        public int WriteFileUnsafe(IntPtr buffer, uint bufferLength, out uint bytesWritten, long offset)
        {
            bytesWritten = 0;
            return HResultConstants.AccessDenied;
        }

        public int WriteFileUnsafeAsync(IntPtr buffer, uint bufferLength, out uint bytesWrittenOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback)
        {
            bytesWrittenOnSyncResult = 0;
            return HResultConstants.AccessDenied;
        }
    }
}
