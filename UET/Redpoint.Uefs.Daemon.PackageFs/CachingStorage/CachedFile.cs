namespace Redpoint.Uefs.Daemon.PackageFs.CachingStorage
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using Redpoint.Uefs.Daemon.RemoteStorage.Pooling;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Windows;
    using System.Collections.Concurrent;
    using System.ComponentModel;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal sealed class CachedFile : ICachedFile, IDisposable
    {
        private readonly ILogger _logger;

        private IRemoteStorageBlobFactory _sourceFactory;
        private string _cacheFile;
        private string _indexFile;
        private const long _chunkSize = 128 * 1024;
        private const long _maximumSourceFileSize = 2L * 1024 * 1024 * 1024 * 1024;
        private ConcurrentBitfield _index;
        private int _dirtyIndex = 0;
        private FileStreamPool _cacheWriteStreamPool;
        private FileStreamPool _cacheReadStreamPool;
        private SafeFileHandlePool _cacheWriteFileHandlePool;
        private SafeFileHandlePool _cacheReadFileHandlePool;
        private ConcurrentBag<byte[]> _chunkBufferPool;
        private ConcurrentBag<IntPtr> _chunkRawBufferPool;
        private readonly long _length;

        public long Length => _length;

        private bool GetIndexBit(uint chunk)
        {
            return _index.Get(chunk);
        }

        private void SetIndexBit(uint chunk)
        {
            _index.SetOn(chunk);
            Interlocked.Exchange(ref _dirtyIndex, 1);
        }

        private static uint GetChunk(long offset)
        {
            return (uint)(offset / _chunkSize);
        }

        private static long GetPosition(uint chunk)
        {
            return chunk * _chunkSize;
        }

        [SupportedOSPlatform("windows6.2")]
        static void MarkAsSparseFile(SafeFileHandle fileHandle)
        {
            int bytesReturned = 0;
            var lpOverlapped = new AsyncIoNativeOverlapped();
            bool result =
                NativeMethods.DeviceIoControl(
                    fileHandle,
                    590020, // FSCTL_SET_SPARSE,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    0,
                    ref bytesReturned,
                    lpOverlapped);
            if (result == false)
                throw new Win32Exception();
        }

        public CachedFile(
            ILogger logger,
            IRemoteStorageBlobFactory sourceFactory,
            string cacheFile,
            string indexFile)
        {
            _logger = logger;
            _sourceFactory = sourceFactory;
            _cacheFile = cacheFile;
            _indexFile = indexFile;

            var initializeNewIndex = true;

            // Try to load the index and cache file from the existing data.
            if (File.Exists(_indexFile))
            {
                try
                {
                    var newIndex = ConcurrentBitfield.LoadFromFile(_indexFile, _maximumSourceFileSize / _chunkSize);
                    if (newIndex != null)
                    {
                        _index = newIndex;
                        initializeNewIndex = false;

                        // No need to touch the cache file.
                    }
                    else
                    {
                        // Index is corrupt, needs re-initialization.
                    }
                }
                catch
                {
                    // Index is corrupt, needs re-initialization.
                }
            }

            // Set up a new index and cache.
            if (initializeNewIndex)
            {
                // Initialize the new index.
                _index = new ConcurrentBitfield(_maximumSourceFileSize / _chunkSize);

                // Initialize the new cache file.
                if (File.Exists(cacheFile))
                {
                    File.Delete(cacheFile);
                }
                using (var cacheStream = new FileStream(cacheFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096))
                {
                    if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
                    {
                        MarkAsSparseFile(cacheStream.SafeFileHandle);
                    }
                    else
                    {
                        // Technically we could permit caching without sparse files (we'd just reserve all the disk space upfront), but I'd rather 
                        // try to get this working on macOS properly before switching macOS over to a caching storage.
                        throw new InvalidOperationException("This operating system does not have a sparse files implementation inside UEFS, which is currently required for caching to work.");
                    }

                    using (var blob = _sourceFactory.Open())
                    {
                        cacheStream.SetLength(blob.Length);
                    }
                }
            }

            if (_index == null)
            {
                throw new InvalidOperationException("_index must be initialized!");
            }

            _cacheWriteStreamPool = new FileStreamPool(cacheFile, FileAccess.Write, FileShare.ReadWrite);
            _cacheReadStreamPool = new FileStreamPool(cacheFile, FileAccess.Read, FileShare.ReadWrite);
            _cacheWriteFileHandlePool = new SafeFileHandlePool(cacheFile, FileAccess.Write, FileShare.ReadWrite);
            _cacheReadFileHandlePool = new SafeFileHandlePool(cacheFile, FileAccess.Read, FileShare.ReadWrite);
            _chunkBufferPool = new ConcurrentBag<byte[]>();
            _chunkRawBufferPool = new ConcurrentBag<IntPtr>();

            _length = new FileInfo(cacheFile).Length;
        }

        public bool VerifyChunks(bool isFixing, Action<Action<PollingResponse>> updatePollingResponse)
        {
            // Technically this can be one more than the actual filesize, but it doesn't matter
            // since we check the bitfield to see what chunks are actually cached and that extra
            // chunk won't ever exist as it's beyond the end of the file.
            var lastChunk = ((new FileInfo(_cacheFile).Length) + _chunkSize) / _chunkSize;
            updatePollingResponse(x =>
            {
                x.VerifyingChunk(lastChunk * _chunkSize);
            });
            for (long i = 0; i < lastChunk; i++)
            {
                updatePollingResponse(x =>
                {
                    x.VerifyingChunkUpdatePosition(i * _chunkSize);
                });
                if (_index.Get((ulong)i))
                {
                    long corruptPosition = 0;
                    byte sourceByte = 0, cacheByte = 0;
                    if (!VerifyChunk((uint)i, ref corruptPosition, ref sourceByte, ref cacheByte))
                    {
                        if (isFixing)
                        {
                            _index.SetOff((uint)i);
                            Interlocked.Exchange(ref _dirtyIndex, 1);
                            updatePollingResponse(x =>
                            {
                                x.VerifyingChunkIncrementFixed();
                            });
                        }
                        else
                        {
                            updatePollingResponse(x =>
                            {
                                x.Error($"'{_cacheFile}' is corrupt - chunk {i} does not match remote source at offset {corruptPosition} (source: {sourceByte} != cache: {cacheByte})!");
                            });
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public bool VerifyChunk(uint chunk, ref long corruptPosition, ref byte sourceByte, ref byte cacheByte)
        {
            var sourceUnsafe = _sourceFactory.OpenUnsafe();
            if (sourceUnsafe != null)
            {
                using (sourceUnsafe)
                {
                    using (var cache = _cacheReadFileHandlePool.Rent())
                    {
                        IntPtr sourceBuffer;
                        IntPtr cacheBuffer;
                        if (!_chunkRawBufferPool.TryTake(out sourceBuffer))
                        {
                            sourceBuffer = Marshal.AllocHGlobal((int)_chunkSize);
                        }
                        if (!_chunkRawBufferPool.TryTake(out cacheBuffer))
                        {
                            cacheBuffer = Marshal.AllocHGlobal((int)_chunkSize);
                        }

                        long position = GetPosition(chunk);

                        uint bytesRead, cacheBytesRead;

                        unsafe
                        {
                            AsyncIoNativeOverlapped sourceOverlapped = new AsyncIoNativeOverlapped
                            {
                                Offset = position,
                                EventHandle = IntPtr.Zero,
                            };
                            if (!NativeMethods.ReadFile(sourceUnsafe.SafeFileHandle, sourceBuffer, (uint)_chunkSize, out bytesRead, (nint)(&sourceOverlapped)))
                            {
                                bytesRead = 0;
                                var hresult = Marshal.GetHRForLastWin32Error();
                                if (hresult != HResultConstants.EOF)
                                {
                                    _logger.LogError($"CachedFile NativeMethods.ReadFile: {position} {hresult:X}");
                                }
                            }
                            AsyncIoNativeOverlapped cacheOverlapped = new AsyncIoNativeOverlapped
                            {
                                Offset = position,
                                EventHandle = IntPtr.Zero,
                            };
                            if (!NativeMethods.ReadFile(cache.SafeFileHandle, cacheBuffer, (uint)bytesRead, out cacheBytesRead, (nint)(&cacheOverlapped)))
                            {
                                bytesRead = 0;
                                var hresult = Marshal.GetHRForLastWin32Error();
                                if (hresult != HResultConstants.EOF)
                                {
                                    _logger.LogError($"CachedFile NativeMethods.ReadFile: {position} {hresult:X}");
                                }
                            }
                        }

                        if (bytesRead != cacheBytesRead)
                        {
                            throw new InvalidOperationException($"Cache did not read the same amount of bytes {cacheBytesRead} as the source {bytesRead}!");
                        }

                        unsafe
                        {
                            int count = Vector<long>.Count;
                            for (int i = 0; i < bytesRead; i += count * sizeof(long))
                            {
                                bool minichunkEqual;
                                if (i + count > bytesRead)
                                {
                                    // Non-vector compare because we don't have enough data left.
                                    long* sourceRaw = (long*)(sourceBuffer + i).ToPointer();
                                    long* cacheRaw = (long*)(cacheBuffer + i).ToPointer();
                                    minichunkEqual = true;
                                    for (int c = 0; c < (bytesRead - (i + count)) / sizeof(long); c += 1)
                                    {
                                        if (sourceRaw[c] != cacheRaw[c])
                                        {
                                            minichunkEqual = false;
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    // Vector compare for speeeed.
                                    Vector<long> sourceRaw = Unsafe.Read<Vector<long>>((sourceBuffer + i).ToPointer());
                                    Vector<long> cacheRaw = Unsafe.Read<Vector<long>>((cacheBuffer + i).ToPointer());
                                    minichunkEqual = Vector.EqualsAll(sourceRaw, cacheRaw);
                                }

                                if (!minichunkEqual)
                                {
                                    corruptPosition = position + (i * sizeof(long));

                                    // Try to figure out a more accurate position.
                                    byte* sourceByteLevel = (byte*)sourceBuffer.ToPointer();
                                    byte* cacheByteLevel = (byte*)cacheBuffer.ToPointer();
                                    for (int z = 0; z < count * sizeof(long); z++)
                                    {
                                        int o = i + z;
                                        if (sourceByteLevel[o] != cacheByteLevel[o])
                                        {
                                            corruptPosition = position + o;
                                            sourceByte = sourceByteLevel[o];
                                            cacheByte = cacheByteLevel[o];
                                            break;
                                        }
                                    }

                                    return false;
                                }
                            }
                        }

                        _chunkRawBufferPool.Add(sourceBuffer);
                        _chunkRawBufferPool.Add(cacheBuffer);
                    }
                }
            }
            else
            {
                using (var source = _sourceFactory.Open())
                {
                    using (var cache = _cacheReadStreamPool.Rent())
                    {
                        byte[] sourceBuffer;
                        byte[] cacheBuffer;
                        if (!_chunkBufferPool.TryTake(out sourceBuffer!))
                        {
                            sourceBuffer = new byte[_chunkSize];
                        }
                        if (!_chunkBufferPool.TryTake(out cacheBuffer!))
                        {
                            cacheBuffer = new byte[_chunkSize];
                        }

                        long position = GetPosition(chunk);
                        cache.FileStream.Position = position;
                        source.Position = position;
                        var bytesRead = source.Read(sourceBuffer, 0, (int)_chunkSize);
                        var cacheBytesRead = cache.FileStream.Read(cacheBuffer, 0, bytesRead);

                        if (bytesRead != cacheBytesRead)
                        {
                            throw new InvalidOperationException($"Cache did not read the same amount of bytes {cacheBytesRead} as the source {bytesRead}!");
                        }

                        for (int i = 0; i < bytesRead; i++)
                        {
                            if (sourceBuffer[i] != cacheBuffer[i])
                            {
                                corruptPosition = position + i;
                                sourceByte = sourceBuffer[i];
                                cacheByte = cacheBuffer[i];
                                return false;
                            }
                        }

                        _chunkBufferPool.Add(sourceBuffer);
                        _chunkBufferPool.Add(cacheBuffer);
                    }
                }
            }

            return true;
        }

        private int EnsureChunk(uint chunk)
        {
            var sourceUnsafe = _sourceFactory.OpenUnsafe();
            if (sourceUnsafe != null)
            {
                using (sourceUnsafe)
                {
                    using (var cache = _cacheWriteFileHandlePool.Rent())
                    {
                        IntPtr buffer;
                        if (!_chunkRawBufferPool.TryTake(out buffer))
                        {
                            buffer = Marshal.AllocHGlobal((int)_chunkSize);
                        }

                        long position = GetPosition(chunk);

                        uint bytesRead, bytesWritten;

                        unsafe
                        {
                            AsyncIoNativeOverlapped readOverlapped = new AsyncIoNativeOverlapped()
                            {
                                Offset = position,
                                EventHandle = IntPtr.Zero,
                            };
                            if (!NativeMethods.ReadFile(sourceUnsafe.SafeFileHandle, buffer, (uint)_chunkSize, out bytesRead, (nint)(&readOverlapped)))
                            {
                                bytesRead = 0;
                                var hresult = Marshal.GetHRForLastWin32Error();
                                if (hresult == HResultConstants.DiskFull)
                                {
                                    _logger.LogError($"EnsureChunk is failing because the local disk is out of disk space (so we can't cache data from the remote server).");
                                }
                                if (hresult != HResultConstants.EOF)
                                {
                                    _logger.LogError($"CachedFile NativeMethods.ReadFile: {position} {hresult:X}");
                                    return hresult;
                                }
                            }
                            AsyncIoNativeOverlapped writeOverlapped = new AsyncIoNativeOverlapped()
                            {
                                Offset = position,
                                EventHandle = IntPtr.Zero,
                            };
                            if (!NativeMethods.WriteFile(cache.SafeFileHandle, buffer, (uint)bytesRead, out bytesWritten, (nint)(&writeOverlapped)))
                            {
                                bytesWritten = 0;
                                var hresult = Marshal.GetHRForLastWin32Error();
                                if (hresult == HResultConstants.DiskFull)
                                {
                                    _logger.LogError($"EnsureChunk is failing because the local disk is out of disk space (so we can't cache data from the remote server).");
                                }
                                if (hresult != HResultConstants.EOF)
                                {
                                    _logger.LogError($"CachedFile NativeMethods.WriteFile: {position} {hresult:X}");
                                    return hresult;
                                }
                            }
                        }

                        NativeMethods.FlushFileBuffers(cache.SafeFileHandle);

                        if (bytesRead != bytesWritten)
                        {
                            _logger.LogError($"Only wrote {bytesWritten} bytes for chunk (raw ptr mode), but read {bytesRead}. This is a bug and will lead to corrupt chunks!");
                        }

                        _chunkRawBufferPool.Add(buffer);
                    }
                }
            }
            else
            {
                using (var source = _sourceFactory.Open())
                {
                    using (var cache = _cacheWriteStreamPool.Rent())
                    {
                        byte[] buffer;
                        if (!_chunkBufferPool.TryTake(out buffer!))
                        {
                            buffer = new byte[_chunkSize];
                        }

                        int bytesRead;

                        long position = GetPosition(chunk);
                        cache.FileStream.Position = position;
                        source.Position = position;
                        bytesRead = source.Read(buffer, 0, (int)_chunkSize);
                        cache.FileStream.Write(buffer, 0, bytesRead);
                        cache.FileStream.Flush();

                        if (cache.FileStream.Position != source.Position)
                        {
                            _logger.LogError($"Only wrote {cache.FileStream.Position - position} bytes for chunk (stream mode), but read {bytesRead}. This is a bug and will lead to corrupt chunks!");
                        }

                        _chunkBufferPool.Add(buffer);
                    }
                }
            }
            return 0;
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

        /// <summary>
        /// For VFS platforms that support it, this directly writes the cache data into the buffer
        /// so that there is no marshalling of byte arrays.
        /// </summary>
        public int ReadFileUnsafe(nint buffer, uint bufferLength, out uint bytesRead, long offset)
        {
            var startingChunk = GetChunk(offset);
            var endingChunk = GetChunk(offset + bufferLength - 1);
            if (startingChunk == endingChunk)
            {
                if (!GetIndexBit(startingChunk))
                {
                    var hresult = EnsureChunk(startingChunk);
                    if (hresult != 0)
                    {
                        bytesRead = 0;
                        return hresult;
                    }
                    SetIndexBit(startingChunk);
                }
            }
            else
            {
                for (var i = startingChunk; i <= endingChunk; i++)
                {
                    if (!GetIndexBit(i))
                    {
                        var hresult = EnsureChunk(i);
                        if (hresult != 0)
                        {
                            bytesRead = 0;
                            return hresult;
                        }
                        SetIndexBit(i);
                    }
                }
            }
            using (var handle = _cacheReadFileHandlePool.Rent())
            {
                unsafe
                {
                    AsyncIoNativeOverlapped overlapped = new AsyncIoNativeOverlapped()
                    {
                        Offset = offset,
                        EventHandle = IntPtr.Zero,
                    };
                    if (!NativeMethods.ReadFile(handle.SafeFileHandle, buffer, bufferLength, out bytesRead, (nint)(&overlapped)))
                    {
                        bytesRead = 0;
                        var hresult = Marshal.GetHRForLastWin32Error();
                        if (hresult != HResultConstants.EOF)
                        {
                            _logger.LogError($"CachedFile NativeMethods.ReadFile: {offset} {hresult:X}");
                        }
                        else
                        {
                            return hresult;
                        }
                    }
                }
                return 0;
            }
        }

        public int ReadFileUnsafeAsync(nint buffer, uint bufferLength, out uint bytesReadOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback)
        {
            // Force caller to use sync I/O.
            bytesReadOnSyncResult = 0;
            return HResultConstants.NotSupported;
        }

        public void FlushIndex()
        {
            if (Interlocked.Exchange(ref _dirtyIndex, 0) == 1)
            {
                _index.SaveToFile(_indexFile);
            }
        }

        public void Dispose()
        {
            _cacheReadStreamPool.Dispose();
            _cacheWriteStreamPool.Dispose();
            foreach (var buffer in _chunkRawBufferPool)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public int WriteFile(byte[] buffer, uint bytesToWrite, out uint bytesWritten, long offset)
        {
            bytesWritten = 0;
            return HResultConstants.AccessDenied;
        }

        public int WriteFileUnsafe(nint buffer, uint bufferLength, out uint bytesWritten, long offset)
        {
            bytesWritten = 0;
            return HResultConstants.AccessDenied;
        }

        public int WriteFileUnsafeAsync(nint buffer, uint bufferLength, out uint bytesWrittenOnSyncResult, long offset, ulong requestHint, IAsyncIoProcessing asyncIo, VfsFileAsyncCallback callback)
        {
            bytesWrittenOnSyncResult = 0;
            return HResultConstants.AccessDenied;
        }

        public int SetEndOfFile(long length)
        {
            return HResultConstants.AccessDenied;
        }
    }
}
