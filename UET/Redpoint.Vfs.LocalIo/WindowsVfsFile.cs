namespace Redpoint.Vfs.LocalIo
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Windows;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal class WindowsVfsFile : IVfsFile, IVfsFileHandle<IVfsFile>, IAsyncIoHandle
    {
        private readonly ILogger _logger;
        private readonly IVfsFileWriteCallbacks? _callbacks;
        private readonly string? _scratchPath;
        private readonly SafeFileHandle _handle;
        private long _lastLength;

        internal WindowsVfsFile(
            ILogger logger,
            string path,
            FileMode fileMode,
            FileAccess fileAccess,
            FileShare fileShare,
            IVfsFileWriteCallbacks? callbacks,
            string? scratchPath)
        {
            _handle = File.OpenHandle(path, fileMode, fileAccess, fileShare);
            _logger = logger;
            _callbacks = callbacks;
            _scratchPath = scratchPath;
            _lastLength = -1;
        }

        public IVfsFile VfsFile => this;

        public SafeFileHandle SafeFileHandle => _handle;

        public long Length
        {
            get
            {
                if (_lastLength == -1)
                {
                    NativeMethods.GetFileSizeEx(_handle, out _lastLength);
                }
                return _lastLength;
            }
        }

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

        public int ReadFileUnsafe(nint buffer, uint bufferLength, out uint bytesRead, long offset)
        {
            unsafe
            {
                AsyncIoNativeOverlapped overlapped = new AsyncIoNativeOverlapped()
                {
                    Offset = offset,
                    EventHandle = IntPtr.Zero,
                };
                if (!NativeMethods.ReadFile(_handle, buffer, bufferLength, out bytesRead, (nint)(&overlapped)))
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

        public int ReadFileUnsafeAsync(nint buffer, uint bufferLength, out uint bytesReadOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback)
        {
            unsafe
            {
                bool release = true;
                AsyncIoNativeOverlapped* overlapped = asyncIo.AllocateNativeOverlapped(this, requestHint, callback);
                var ptr = (nint)overlapped;
                try
                {
                    overlapped->Offset = offset;
                    if (!NativeMethods.ReadFile(_handle, buffer, bufferLength, out bytesReadOnSyncResult, ptr))
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

        public int WriteFile(byte[] buffer, uint bytesToWrite, out uint bytesWritten, long offset)
        {
            GCHandle pinnedArray = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                bytesToWrite = Math.Min(bytesToWrite, (uint)buffer.Length);
                return WriteFileUnsafe(
                    pinnedArray.AddrOfPinnedObject(),
                    bytesToWrite,
                    out bytesWritten,
                    offset);
            }
            finally
            {
                pinnedArray.Free();
            }
        }

        public int WriteFileUnsafe(nint buffer, uint bufferLength, out uint bytesWritten, long offset)
        {
            unsafe
            {
                AsyncIoNativeOverlapped overlapped = new AsyncIoNativeOverlapped()
                {
                    Offset = offset,
                    EventHandle = IntPtr.Zero,
                };
                if (!NativeMethods.WriteFile(_handle, buffer, bufferLength, out bytesWritten, (nint)(&overlapped)))
                {
                    bytesWritten = 0;
                    var hresult = Marshal.GetHRForLastWin32Error();
                    if (hresult == HResultConstants.EOF)
                    {
                        // This is fine, it's a normal EOF.
                        if (_callbacks != null)
                        {
                            _callbacks.OnObjectModifiedAtRelativePath(_scratchPath!);
                        }
                        return 0;
                    }
                    _logger.LogError($"NativeMethods.WriteFile: {offset} {hresult:X}");
                    return hresult;
                }
                if (offset + bytesWritten > _lastLength)
                {
                    _lastLength = offset + bytesWritten;
                }
                if (_callbacks != null)
                {
                    _callbacks.OnObjectModifiedAtRelativePath(_scratchPath!);
                }
                return 0;
            }
        }

        public int WriteFileUnsafeAsync(nint buffer, uint bufferLength, out uint bytesWrittenOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback)
        {
            unsafe
            {
                bool release = true;
                AsyncIoNativeOverlapped* overlapped = asyncIo.AllocateNativeOverlapped(this, requestHint, (requestHint, status, bytesTransferred) =>
                {
                    if (offset + bytesTransferred > _lastLength)
                    {
                        _lastLength = offset + bytesTransferred;
                    }
                    if (_callbacks != null)
                    {
                        _callbacks.OnObjectModifiedAtRelativePath(_scratchPath!);
                    }
                    callback(requestHint, status, bytesTransferred);
                });
                var ptr = (nint)overlapped;
                try
                {
                    overlapped->Offset = offset;
                    if (!NativeMethods.WriteFile(_handle, buffer, bufferLength, out bytesWrittenOnSyncResult, ptr))
                    {
                        bytesWrittenOnSyncResult = 0;
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
                        else
                        {
                            if (_callbacks != null)
                            {
                                _callbacks.OnObjectModifiedAtRelativePath(_scratchPath!);
                            }
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
            unsafe
            {
                NativeMethods.NativeFileEndOfFileInfo eof = new NativeMethods.NativeFileEndOfFileInfo
                {
                    EndOfFile = length,
                };
                if (!NativeMethods.SetFileInformationByHandle(_handle, NativeMethods.FileInformationClass_FileEndOfFileInfo, eof, sizeof(NativeMethods.NativeFileEndOfFileInfo)))
                {
                    return Marshal.GetHRForLastWin32Error();
                }
                if (_callbacks != null)
                {
                    _callbacks.OnObjectModifiedAtRelativePath(_scratchPath!);
                }
                _lastLength = length;
                return 0;
            }
        }
    }
}
