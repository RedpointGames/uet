namespace Redpoint.Vfs.Driver.WinFsp
{
    using Fsp;
    using Fsp.Interop;
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Windows;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Security.AccessControl;
    using System.Threading;
    using FileAccess = FileAccess;
    using FileInfo = Fsp.Interop.FileInfo;

    [SupportedOSPlatform("windows6.2")]
    internal class WinFspVfsDriverImpl : FileSystemBase, IAsyncIoProcessing
    {
        private readonly ILogger _logger;
        private readonly IVfsLayer _projectionLayer;
        private readonly bool _enableCorrectnessChecks;
        private readonly SemaphoreSlim _loggerSemaphore;
        private readonly DateTimeOffset _rootTime;
        private readonly IComparer<string> _caseInsensitiveComparer = new FileSystemNameComparer();
        private readonly AutoResetEvent _pendingAsyncIo;
        private readonly ConcurrentDictionary<nint, (ulong requestHint, IAsyncIoHandle ioHandle, VfsFileAsyncCallback callback)> _pendingNativeOverlapped;
        private readonly Thread _asyncIoThread;
        private readonly bool _enableAsyncIo;
        private readonly bool _enableNameNormalization;

        private bool _running = true;

        private const uint _dataWriteAccess = 0x2 | 0x4 | 0x10000 | 0x40000000;

        protected const int _allocationUnit = 4096;

        private readonly byte[] _rootSecurityDescriptor;
        private readonly byte[] _fileSecurityDescriptor;
        private readonly byte[] _directorySecurityDescriptor;

        public WinFspVfsDriverImpl(
            ILogger logger,
            IVfsLayer projectionLayer,
            WinFspVfsDriverOptions driverOptions)
        {
            _logger = logger;
            _projectionLayer = projectionLayer;
            _enableCorrectnessChecks = driverOptions.EnableCorrectnessChecks;
            _loggerSemaphore = new SemaphoreSlim(1);
            _rootTime = DateTimeOffset.UtcNow;

            _pendingAsyncIo = new AutoResetEvent(false);
            _pendingNativeOverlapped = new ConcurrentDictionary<nint, (ulong requestHint, IAsyncIoHandle ioHandle, VfsFileAsyncCallback callback)>();
            _asyncIoThread = new Thread(RunAsyncIo);
            _enableAsyncIo = driverOptions.EnableAsyncIo;
            if (_enableAsyncIo)
            {
                _asyncIoThread.Start();
            }
            _enableNameNormalization = driverOptions.EnableNameNormalization;

            var securityDescriptor = new RawSecurityDescriptor("O:WDG:WDD:PAI(A;;FA;;;WD)");
            _rootSecurityDescriptor = new byte[securityDescriptor.BinaryLength];
            securityDescriptor.GetBinaryForm(_rootSecurityDescriptor, 0);

            _fileSecurityDescriptor = _rootSecurityDescriptor;
            _directorySecurityDescriptor = _rootSecurityDescriptor;
        }

        public FileSystemHost? FileSystemHost { get; set; }

        private unsafe void DispatchAsyncIo(
            nint overlappedPtr,
            IAsyncIoHandle ioHandle,
            ulong requestHint,
            VfsFileAsyncCallback callback)
        {
            var overlapped = (AsyncIoNativeOverlapped*)overlappedPtr;

            uint bytesTransferred;
            try
            {
                if (!NativeMethods.GetOverlappedResult(
                    ioHandle.SafeFileHandle,
                    (nint)overlapped,
                    out bytesTransferred,
                    true))
                {
                    // Error or EOF state.
                    var hresult = Marshal.GetHRForLastWin32Error();
                    callback(
                        requestHint,
                        StatusConvert.ConvertHResultToNTSTATUS(hresult),
                        bytesTransferred);
                }
                else
                {
                    // Operation completed normally.
                    callback(
                        requestHint,
                        0x0,
                        bytesTransferred);
                }
            }
            catch (ObjectDisposedException)
            {
                // The file handle was closed before the async I/O operation
                // completed, therefore the operation was cancelled.
                bytesTransferred = 0;
                callback(
                    requestHint,
                    NTSTATUSConstants.Cancelled,
                    bytesTransferred);
            }
            ReleaseNativeOverlapped(overlappedPtr);
        }

        private void RunAsyncIo()
        {
            unsafe
            {
                while (_running)
                {
                    try
                    {
                        _pendingAsyncIo.WaitOne();

                        // If we stopped running, exit immediately.
                        if (!_running)
                        {
                            return;
                        }

                        // Iterate through all pending async operations and find
                        // the first one that is no longer pending.
                        foreach (var kv in _pendingNativeOverlapped)
                        {
                            var overlapped = (AsyncIoNativeOverlapped*)kv.Key;
                            if (overlapped->Status != NTSTATUSConstants.Pending)
                            {
                                if (_pendingNativeOverlapped.TryRemove(kv.Key, out var value))
                                {
                                    DispatchAsyncIo(
                                        kv.Key,
                                        value.ioHandle,
                                        value.requestHint,
                                        value.callback);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error while handling async I/O: {ex.Message}");
                    }
                }
            }
        }

        public unsafe AsyncIoNativeOverlapped* AllocateNativeOverlapped(IAsyncIoHandle ioHandle, ulong requestHint, VfsFileAsyncCallback callback)
        {
            AsyncIoNativeOverlapped* overlapped = (AsyncIoNativeOverlapped*)Marshal.AllocHGlobal(sizeof(AsyncIoNativeOverlapped));
            // _logger.LogError($"Allocated OVERLAPPED: {(nint)overlapped}");
            if (!_pendingNativeOverlapped.TryAdd((nint)overlapped, (requestHint, ioHandle, callback)))
            {
                _logger.LogError("Failed to add pending async I/O to dictionary!");
                throw new InvalidOperationException("Failed to add pending async I/O to dictionary!");
            }
            overlapped->EventHandle = _pendingAsyncIo.SafeWaitHandle.DangerousGetHandle();
            return overlapped;
        }

        public unsafe void CancelNativeOverlapped(nint overlapped)
        {
            if (_pendingNativeOverlapped.TryRemove(overlapped, out _))
            {
                ReleaseNativeOverlapped(overlapped);
            }
        }

        private unsafe void ReleaseNativeOverlapped(nint overlapped)
        {
            // _logger.LogError($"Freeing OVERLAPPED: {overlapped}");
            Marshal.FreeHGlobal(overlapped);
        }

        public override void Unmounted(object Host)
        {
            _running = false;
            _pendingAsyncIo.Set(); // So that it can pick up _running.
            _asyncIoThread.Join();
            base.Unmounted(Host);
        }

        private int Trace(string fileName, int result, object?[]? parameters = null, [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
        {
#if !ENABLE_TRACE_LOGS
            if (result == STATUS_SUCCESS || result == STATUS_END_OF_FILE || result == STATUS_OBJECT_PATH_NOT_FOUND || result == STATUS_OBJECT_NAME_NOT_FOUND)
            {
                return result;
            }
#endif

            var extraParameters = parameters != null && parameters.Length > 0
                ? ", " + string.Join(", ", parameters.Select(x => x?.ToString() ?? string.Empty))
                : string.Empty;

            _loggerSemaphore.Wait();
            try
            {
                if (result == STATUS_SUCCESS || result == STATUS_END_OF_FILE)
                {
#if ENABLE_TRACE_LOGS
                    _logger.LogTrace($"{memberName}: 0x{unchecked((uint)result):X} = {fileName} ({extraParameters}) [{lineNumber}]");
#endif
                }
                else if (result == STATUS_OBJECT_PATH_NOT_FOUND || result == STATUS_OBJECT_NAME_NOT_FOUND)
                {
#if ENABLE_TRACE_LOGS
                    _logger.LogWarning($"{memberName}: 0x{unchecked((uint)result):X} = {fileName} ({extraParameters}) [{lineNumber}]");
#endif
                }
                else
                {
                    _logger.LogError($"{memberName}: 0x{unchecked((uint)result):X} = {fileName} ({extraParameters}) [{lineNumber}]");
                }
            }
            finally
            {
                _loggerSemaphore.Release();
            }
            return result;
        }

        internal static ulong GetAllocationSize(ulong fileSize)
        {
            return (fileSize + _allocationUnit - 1) / _allocationUnit * _allocationUnit;
        }

        public override int Init(object hostRaw)
        {
            var host = (FileSystemHost)hostRaw;
            host.SectorSize = _allocationUnit;
            host.SectorsPerAllocationUnit = 1;
            host.MaxComponentLength = 255;
            host.FileInfoTimeout = 1000;
            host.CaseSensitiveSearch = false;
            host.CasePreservedNames = true;
            host.UnicodeOnDisk = true;
            host.PersistentAcls = true;
            host.PostCleanupWhenModifiedOnly = true;
            host.PassQueryDirectoryPattern = true;
            host.FlushAndPurgeOnCleanup = true;
            host.VolumeCreationTime = (UInt64)_rootTime.ToFileTime();
            host.VolumeSerialNumber = 0;
            return STATUS_SUCCESS;
        }

        public override int GetVolumeInfo(out VolumeInfo volumeInfo)
        {
            volumeInfo = default(VolumeInfo);
            volumeInfo.TotalSize = 2L * 1024 * 1024 * 1024;
            volumeInfo.FreeSize = 256 * 1024 * 1024;
            volumeInfo.SetVolumeLabel("GitProjection");
            return Trace(string.Empty, STATUS_SUCCESS);
        }

        public override int GetSecurityByName(
            string fileName,
            out uint fileAttributes,
            ref byte[] securityDescriptor)
        {
            try
            {
                fileName = fileName.TrimStart('\\');

                if (fileName == string.Empty)
                {
                    fileAttributes = (uint)FileAttributes.Directory;
                    if (securityDescriptor != null)
                    {
                        securityDescriptor = _rootSecurityDescriptor;
                    }
                    return Trace(fileName, STATUS_SUCCESS);
                }
                else
                {
                    var exists = _projectionLayer.Exists(fileName);
                    if (exists == VfsEntryExistence.FileExists)
                    {
                        fileAttributes = (uint)FileAttributes.Archive;
                        if (securityDescriptor != null)
                        {
                            securityDescriptor = _fileSecurityDescriptor;
                        }
                        return Trace(fileName, STATUS_SUCCESS);
                    }
                    else if (exists == VfsEntryExistence.DirectoryExists)
                    {
                        fileAttributes = (uint)FileAttributes.Directory;
                        if (securityDescriptor != null)
                        {
                            securityDescriptor = _directorySecurityDescriptor;
                        }
                        return Trace(fileName, STATUS_SUCCESS);
                    }
                    else
                    {
                        fileAttributes = 0;
                        return Trace(fileName, STATUS_OBJECT_NAME_NOT_FOUND);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        private string? GetNormalizedName(string path)
        {
            // @note: We should have this API on the projection layers, since
            // they will be able to answer it much faster than this function.
            // scratch - check if materialized, if it is just get FullName
            // for on disk file. otherwise delegate to next layer.
            // git layer should be able to just it proper capitalization
            // from it's store.
            if (_enableNameNormalization)
            {
                if (path == string.Empty)
                {
                    return @"\";
                }
                else
                {
                    var components = path.Split('\\');
                    var currentPath = string.Empty;
                    var normalizedPath = @"\";
                    foreach (var component in components)
                    {
                        if (currentPath == string.Empty)
                        {
                            currentPath = component;
                        }
                        else
                        {
                            currentPath += $@"\{component}";
                        }
                        var target = _projectionLayer.GetInfo(currentPath);
                        if (target != null)
                        {
                            normalizedPath += $@"{target.Name}\";
                        }
                        else
                        {
                            normalizedPath += $@"{component}\";
                        }
                    }
                    return normalizedPath.TrimEnd('\\');
                }
            }
            else
            {
                return null;
            }
        }

        public override int Create(
            string fileName,
            uint createOptions,
            uint grantedAccess,
            uint fileAttributes,
            byte[] securityDescriptor,
            ulong allocationSize,
            out object? fileNodeOut,
            out object? fileDesc,
            out FileInfo fileInfo,
            out string? normalizedName)
        {
            try
            {
                fileName = fileName.TrimStart('\\');

                fileNodeOut = null;
                fileDesc = null;
                fileInfo = default(FileInfo);
                normalizedName = null;

                // Is this read-only?
                if (_projectionLayer.ReadOnly)
                {
                    return Trace(fileName, STATUS_ACCESS_DENIED);
                }

                // Does this object already exist, or does it's parent not exist?
                if (_projectionLayer.Exists(fileName) == VfsEntryExistence.FileExists)
                {
                    return Trace(fileName, STATUS_OBJECT_NAME_COLLISION);
                }
                var parentPath = Path.GetDirectoryName(fileName);
                if (fileName.Contains('\\') && parentPath != null && (_projectionLayer.Exists(parentPath) != VfsEntryExistence.DirectoryExists))
                {
                    if (_projectionLayer.Exists(parentPath) == VfsEntryExistence.FileExists)
                    {
                        return Trace(fileName, STATUS_NOT_A_DIRECTORY);
                    }
                    else
                    {
                        return Trace(fileName, STATUS_OBJECT_PATH_NOT_FOUND);
                    }
                }

                // Are we creating a directory?
                var isDirectory = (createOptions & FILE_DIRECTORY_FILE) != 0;
                if (isDirectory)
                {
                    // Yes, we're making a directory.
                    var fileNode = WinFspFileNode.AsNewDirectoryWithPath(fileName, DateTimeOffset.UtcNow);

                    // Try to create it.
                    if (!_projectionLayer.CreateDirectory(fileName))
                    {
                        return Trace(fileName, STATUS_ACCESS_DENIED);
                    }

                    // Assign output parameters and return success.
                    fileNodeOut = fileNode;
                    fileDesc = null;
                    fileInfo = fileNode.FileInfo;
                    normalizedName = GetNormalizedName(fileName);
                    return Trace(fileName, STATUS_SUCCESS);
                }
                else
                {
                    // No, we're making a file, try to create it.
                    VfsEntry? metadata = null;
                    var newFileHandle = _projectionLayer.OpenFile(
                        fileName,
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite | FileShare.Delete,
                        ref metadata);
                    if (newFileHandle == null)
                    {
                        return Trace(fileName, STATUS_ACCESS_DENIED);
                    }

                    // Set it's length if needed.
                    if (allocationSize != 0)
                    {
                        newFileHandle.VfsFile.SetEndOfFile((long)allocationSize);
                    }

                    // Create the handle for the file node we're about to return. Create()
                    // creates the file and opens it.
                    var fileNode = WinFspFileNode.AsFileWithPath(
                        fileName,
                        newFileHandle,
                        metadata!);

                    // Assign output parameters and return success.
                    fileNodeOut = fileNode;
                    fileDesc = null;
                    fileInfo = fileNode.FileInfo;
                    normalizedName = GetNormalizedName(fileName);
                    return Trace(fileName, STATUS_SUCCESS);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int Open(
            string fileName,
            uint createOptions,
            uint grantedAccess,
            out object? fileNodeOut,
            out object? fileDesc,
            out FileInfo fileInfo,
            out string? normalizedName)
        {
            try
            {
                fileName = fileName.TrimStart('\\');

                fileNodeOut = null;
                fileDesc = null;
                fileInfo = default(FileInfo);
                normalizedName = null;

                // Is this read-only?
                var isReadWriteRequest = (grantedAccess & _dataWriteAccess) != 0;
                if (_projectionLayer.ReadOnly && isReadWriteRequest)
                {
                    return Trace(fileName, STATUS_ACCESS_DENIED);
                }

                // Are we opening the root?
                if (fileName == string.Empty)
                {
                    // Yes, we're opening the root.
                    var fileNode = WinFspFileNode.AsExistingDirectoryWithPath(fileName, new VfsEntry
                    {
                        Name = "\\",
                        Attributes = FileAttributes.Directory,
                        CreationTime = _rootTime,
                        LastAccessTime = _rootTime,
                        LastWriteTime = _rootTime,
                        ChangeTime = _rootTime,
                        Size = 0
                    });
                    fileNodeOut = fileNode;
                    fileDesc = null;
                    fileInfo = fileNode.FileInfo;
                    normalizedName = GetNormalizedName(fileName);
                    return Trace(fileName, STATUS_SUCCESS);
                }
                // Are we opening a directory?
                else if (_projectionLayer.Exists(fileName) == VfsEntryExistence.DirectoryExists)
                {
                    // Yes, we're opening a directory.
                    var info = _projectionLayer.GetInfo(fileName);
                    if (info == null)
                    {
                        info = _projectionLayer.GetInfo(fileName);
                    }
                    var fileNode = WinFspFileNode.AsExistingDirectoryWithPath(fileName, info!);
                    fileNodeOut = fileNode;
                    fileDesc = null;
                    fileInfo = fileNode.FileInfo;
                    normalizedName = GetNormalizedName(fileName);
                    return Trace(fileName, STATUS_SUCCESS);
                }
                else
                {
                    // Otherwise we're probably trying to open a file.
                    // Try to get the handle from the projection layer.
                    VfsEntry? metadata = null;
                    var handle = _projectionLayer.OpenFile(
                        fileName,
                        FileMode.Open,
                        isReadWriteRequest ? FileAccess.ReadWrite : FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete,
                        ref metadata);
                    if (handle == null)
                    {
                        // If we couldn't get a handle, then the object does not exist.
                        return Trace(fileName, STATUS_OBJECT_NAME_NOT_FOUND);
                    }
                    if (metadata == null)
                    {
                        _logger.LogError("OpenFile operation returned handle but no metadata!");
                        handle.Dispose();
                        return Trace(fileName, STATUS_OBJECT_NAME_NOT_FOUND);
                    }

                    // Create the file node based on the handle.
                    var fileNode = WinFspFileNode.AsFileWithPath(
                        fileName,
                        handle,
                        metadata);

                    // Assign output parameters and return success.
                    fileNodeOut = fileNode;
                    fileDesc = null;
                    fileInfo = fileNode.FileInfo;
                    normalizedName = GetNormalizedName(fileName);
                    return Trace(fileName, STATUS_SUCCESS);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int Overwrite(
            object fileNode,
            object fileDesc,
            uint fileAttributes,
            bool replaceFileAttributes,
            ulong allocationSize,
            out FileInfo fileInfo)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;
                fileInfo = default(FileInfo);

                // Is this read-only?
                if (_projectionLayer.ReadOnly)
                {
                    return Trace(fspFileNode.Path, STATUS_ACCESS_DENIED);
                }

                // You can't perform this operation on a directory.
                if (fspFileNode.IsDirectory || fspFileNode.ProjectedFileHandle == null)
                {
                    return Trace(fspFileNode.Path, STATUS_FILE_IS_A_DIRECTORY);
                }

                // @todo: Handle allocation sizes.

                // Truncate file.
                fspFileNode.ProjectedFileHandle.VfsFile.SetEndOfFile(0);
                fspFileNode.FileInfo.FileSize = 0;
                fspFileNode.FileInfo.AllocationSize = 0;

                fileInfo = fspFileNode.FileInfo;
                return Trace(fspFileNode.Path, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override void Cleanup(
            object fileNode,
            object? fileDesc,
            string fileName,
            uint flags)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;

                // There is nothing to be done on cleanup if the projection is read-only.
                if (_projectionLayer.ReadOnly)
                {
                    Trace(fspFileNode.Path, STATUS_SUCCESS);
                    return;
                }

                // The only cleanup flag that is actually relevant to projection layers is
                // the delete flag, where we're deleting a file or directory.
                if ((flags & CleanupDelete) != 0)
                {
                    if (fspFileNode.IsDirectory)
                    {
                        _projectionLayer.DeleteDirectory(fspFileNode.Path);
                    }
                    else
                    {
                        _projectionLayer.DeleteFile(fspFileNode.Path);
                    }
                }

                // There is nothing else to do. In particular:
                // - CleanupSetArchiveBit: All files always have the archive bit.
                // - CleanupSet*Time: Times are controlled by actual write and create operations.
                // - CleanupSetAllocationSize: We don't support allocation size (all files are always their actual size).

                Trace(fspFileNode.Path, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override void Close(
            object fileNode,
            object? fileDesc)
        {
            try
            {
                // Dispose the file node, which will dispose any underlying projected handle
                // if it exists.
                var fspFileNode = (WinFspFileNode)fileNode;
                fspFileNode.Dispose();

                Trace(fspFileNode.Path, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        private void OnAsyncReadComplete(
            ulong requestHint,
            int status,
            uint bytesTransferred)
        {
            FileSystemHost!.SendReadResponse(
                requestHint,
                status,
                bytesTransferred);
        }

        public override int Read(
            object fileNode,
            object? fileDesc,
            IntPtr buffer,
            ulong offset,
            uint length,
            out uint bytesTransferred)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;
                bytesTransferred = 0;

                // You can't perform this operation on a directory.
                if (fspFileNode.IsDirectory || fspFileNode.ProjectedFileHandle == null)
                {
                    return Trace(fspFileNode.Path, STATUS_FILE_IS_A_DIRECTORY);
                }

                // Are we trying to read past the end of the file?
                if (offset >= (ulong)fspFileNode.ProjectedFileHandle.VfsFile.Length)
                {
                    return Trace(fspFileNode.Path, STATUS_END_OF_FILE);
                }

                // Read from the projected file handle.
                int result = HResultConstants.InvalidFunction;
                if (_enableAsyncIo)
                {
                    result = fspFileNode.ProjectedFileHandle.VfsFile.ReadFileUnsafeAsync(
                        buffer,
                        length,
                        out bytesTransferred,
                        (long)offset,
                        FileSystemHost!.GetOperationRequestHint(),
                        this,
                        OnAsyncReadComplete);
                }
                if (!_enableAsyncIo || result == HResultConstants.NotSupported)
                {
                    result = fspFileNode.ProjectedFileHandle.VfsFile.ReadFileUnsafe(
                        buffer,
                        length,
                        out bytesTransferred,
                        (long)offset);
                }
                return Trace(fspFileNode.Path, StatusConvert.ConvertHResultToNTSTATUS(result));
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int Write(
            object fileNode,
            object? fileDesc,
            IntPtr buffer,
            ulong offset,
            uint length,
            bool writeToEndOfFile,
            bool constrainedIo,
            out uint bytesTransferred,
            out FileInfo fileInfo)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;
                bytesTransferred = 0;
                fileInfo = default(FileInfo);

                // Is this read-only?
                if (_projectionLayer.ReadOnly)
                {
                    return Trace(fspFileNode.Path, STATUS_ACCESS_DENIED);
                }

                // You can't perform this operation on a directory.
                if (fspFileNode.IsDirectory || fspFileNode.ProjectedFileHandle == null)
                {
                    return Trace(fspFileNode.Path, STATUS_FILE_IS_A_DIRECTORY);
                }

                // Is this a constrained write (i.e. don't extend the file?)
                var fileLength = (ulong)fspFileNode.ProjectedFileHandle.VfsFile.Length;
                if (constrainedIo)
                {
                    // If we would start writing past the end of the file, there's nothing to do.
                    if (offset >= fileLength)
                    {
                        bytesTransferred = 0;
                        return Trace(fspFileNode.Path, STATUS_SUCCESS);
                    }

                    // Otherwise constrain the length so it doesn't go past the end of the file.
                    if (offset + length > fileLength)
                    {
                        length = (uint)(fileLength - offset);
                    }
                }
                // Are we implicitly writing to the end of the file?
                else if (writeToEndOfFile)
                {
                    offset = fileLength;
                }

                // Write to the projected file handle.
                int result = HResultConstants.InvalidFunction;
                if (_enableAsyncIo)
                {
                    // @todo: Optimize this callback.
                    result = fspFileNode.ProjectedFileHandle.VfsFile.WriteFileUnsafeAsync(
                        buffer,
                        length,
                        out bytesTransferred,
                        (long)offset,
                        FileSystemHost!.GetOperationRequestHint(),
                        this,
                        (ulong requestHint, int status, uint bytesTransferred) =>
                        {
                            if (status == 0x0)
                            {
                                // Refresh the file info from the latest file size.
                                fspFileNode.FileInfo.FileSize = (ulong)fspFileNode.ProjectedFileHandle.VfsFile.Length;
                                fspFileNode.FileInfo.AllocationSize = GetAllocationSize(fspFileNode.FileInfo.FileSize);
                            }
                            FileSystemHost.SendWriteResponse(
                                requestHint,
                                status,
                                bytesTransferred,
                                ref fspFileNode.FileInfo);
                        });
                }
                if (!_enableAsyncIo || result == HResultConstants.NotSupported)
                {
                    result = fspFileNode.ProjectedFileHandle.VfsFile.WriteFileUnsafe(
                        buffer,
                        length,
                        out bytesTransferred,
                        (long)offset);
                    if (result == 0x0)
                    {
                        // Refresh the file info from the latest file size.
                        fspFileNode.FileInfo.FileSize = (ulong)fspFileNode.ProjectedFileHandle.VfsFile.Length;
                        fspFileNode.FileInfo.AllocationSize = GetAllocationSize(fspFileNode.FileInfo.FileSize);
                    }
                    fileInfo = fspFileNode.FileInfo;
                    return Trace(fspFileNode.Path, result);
                }
                return Trace(fspFileNode.Path, StatusConvert.ConvertHResultToNTSTATUS(result));
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int Flush(
            object fileNode,
            object? fileDesc,
            out FileInfo fileInfo)
        {
            try
            {
                var fspFileNode = (WinFspFileNode?)fileNode;

                if (fspFileNode != null)
                {
                    // @todo: Actually call Flush for scratch files.

                    fileInfo = fspFileNode.FileInfo;
                }
                else
                {
                    fileInfo = default(FileInfo);
                }

                return Trace(fspFileNode?.Path ?? string.Empty, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int GetFileInfo(
            object fileNode,
            object? fileDesc,
            out FileInfo fileInfo)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;

                // Refresh file size in case it's out-of-date and then return the file info.
                if (fspFileNode.ProjectedFileHandle != null)
                {
                    fspFileNode.FileInfo.FileSize = (ulong)fspFileNode.ProjectedFileHandle.VfsFile.Length;
                    fspFileNode.FileInfo.AllocationSize = GetAllocationSize(fspFileNode.FileInfo.FileSize);
                }
                fileInfo = fspFileNode.FileInfo;

                return Trace(fspFileNode.Path, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int SetBasicInfo(
            object fileNode,
            object? fileDesc,
            uint fileAttributes,
            ulong creationTime,
            ulong lastAccessTime,
            ulong lastWriteTime,
            ulong changeTime,
            out FileInfo fileInfo)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;
                fileInfo = default(FileInfo);

                uint? newFileAttributes = fileAttributes == uint.MaxValue ? null : fileAttributes;
                DateTimeOffset? newCreationTime = creationTime == 0 ? null : DateTimeOffset.FromFileTime((long)creationTime);
                DateTimeOffset? newLastAccessTime = lastAccessTime == 0 ? null : DateTimeOffset.FromFileTime((long)lastAccessTime);
                DateTimeOffset? newLastWriteTime = lastWriteTime == 0 ? null : DateTimeOffset.FromFileTime((long)lastWriteTime);
                DateTimeOffset? newChangeTime = changeTime == 0 ? null : DateTimeOffset.FromFileTime((long)changeTime);

                // Is this read-only?
                if (_projectionLayer.ReadOnly)
                {
                    return Trace(fspFileNode.Path, STATUS_ACCESS_DENIED);
                }

                // Set the basic information of the entry.
                if (!_projectionLayer.SetBasicInfo(
                    fspFileNode.Path,
                    newFileAttributes,
                    newCreationTime,
                    newLastAccessTime,
                    newLastWriteTime,
                    newChangeTime))
                {
                    return Trace(fspFileNode.Path, STATUS_ACCESS_DENIED);
                }

                // Update info on the underlying FileInfo as well.
                if (newFileAttributes != null)
                {
                    fspFileNode.FileInfo.FileAttributes = newFileAttributes.Value;
                }
                if (newCreationTime != null)
                {
                    fspFileNode.FileInfo.CreationTime = (ulong)newCreationTime.Value.ToFileTime();
                }
                if (newLastAccessTime != null)
                {
                    fspFileNode.FileInfo.LastAccessTime = (ulong)newLastAccessTime.Value.ToFileTime();
                }
                if (newLastWriteTime != null)
                {
                    fspFileNode.FileInfo.LastWriteTime = (ulong)newLastWriteTime.Value.ToFileTime();
                }
                if (newChangeTime != null)
                {
                    fspFileNode.FileInfo.ChangeTime = (ulong)newChangeTime.Value.ToFileTime();
                }

                return Trace(fspFileNode.Path, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int SetFileSize(
            object fileNode,
            object? fileDesc,
            ulong newSize,
            bool setAllocationSize,
            out FileInfo fileInfo)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;
                fileInfo = default(FileInfo);

                // Is this read-only?
                if (_projectionLayer.ReadOnly)
                {
                    return Trace(fspFileNode.Path, STATUS_ACCESS_DENIED);
                }

                // Can't perform this operation on directories.
                if (fspFileNode.IsDirectory || fspFileNode.ProjectedFileHandle == null)
                {
                    return Trace(fspFileNode.Path, STATUS_FILE_IS_A_DIRECTORY);
                }

                // Set the file size on the projected file.
                var result = fspFileNode.ProjectedFileHandle.VfsFile.SetEndOfFile((long)newSize);
                if (result == 0x0)
                {
                    // File resized, update the file length and return the latest file info.
                    fspFileNode.FileInfo.FileSize = setAllocationSize ? GetAllocationSize(newSize) : newSize;
                    fspFileNode.FileInfo.AllocationSize = GetAllocationSize(newSize);
                }
                fileInfo = fspFileNode.FileInfo;
                return Trace(fspFileNode.Path, result);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int CanDelete(
            object fileNode,
            object? fileDesc,
            string fileName)
        {
            try
            {
                // Typically if the target was a directory, we'd check if the directory
                // was empty and return STATUS_DIRECTORY_NOT_EMPTY. But we don't practically
                // need to implement this semantic since we always recursively delete
                // directories in writable projection layers.
                return Trace(string.Empty, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int Rename(
            object fileNode,
            object? fileDesc,
            string fileName,
            string newFileName,
            bool replaceIfExists)
        {
            try
            {
                fileName = fileName.TrimStart('\\');
                newFileName = newFileName.TrimStart('\\');

                // We don't use the file node here.

                // Is this read-only?
                if (_projectionLayer.ReadOnly)
                {
                    return Trace($"{fileName} -> {newFileName}", STATUS_ACCESS_DENIED);
                }

                if (_projectionLayer.MoveFile(
                    fileName,
                    newFileName,
                    replaceIfExists))
                {
                    return Trace($"{fileName} -> {newFileName}", STATUS_SUCCESS);
                }
                else if (_projectionLayer.Exists(newFileName) == VfsEntryExistence.FileExists && !replaceIfExists)
                {
                    return Trace($"{fileName} -> {newFileName}", STATUS_OBJECT_NAME_COLLISION);
                }
                else
                {
                    return Trace($"{fileName} -> {newFileName}", STATUS_ACCESS_DENIED);
                }
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int GetSecurity(
            object fileNode,
            object? fileDesc,
            ref byte[] securityDescriptor)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;

                if (fspFileNode.Path == "\\")
                {
                    securityDescriptor = _rootSecurityDescriptor;
                }
                else if (fspFileNode.IsDirectory)
                {
                    securityDescriptor = _directorySecurityDescriptor;
                }
                else
                {
                    securityDescriptor = _fileSecurityDescriptor;
                }

                return Trace(string.Empty, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int SetSecurity(
            object fileNode,
            object? fileDesc,
            AccessControlSections sections,
            byte[] securityDescriptor)
        {
            try
            {
                // We just pretend to do this so applications don't fail. In reality,
                // our security descriptor is always effectively "Everyone".
                return Trace(string.Empty, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override void ReadDirectoryEntries(
            object fileNode,
            object? fileDesc,
            string pattern,
            string? marker,
            ref object context,
            Func<string, FileInfo, bool> callback)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;

                // We can't perform this operation on non-directories.
                if (!fspFileNode.IsDirectory)
                {
                    return;
                }

                // Get the enumerable for iteration.
                var enumerable = _projectionLayer.List(fspFileNode.Path);
                if (enumerable == null)
                {
                    // This directory does not exist.
                    return;
                }

#if ENABLE_TRACE_LOGS
                var guid = Guid.NewGuid();
                _logger.LogInformation($"{guid} Scanning: {fspFileNode.Path} (marker: {(marker == null ? "(null)" : $"'{marker}'")}, pattern: {(pattern == null ? "(null)" : $"'{pattern}'")})");
#endif

                // Get information about the current directory, and parent directory
                // if applicable.
                var creationTime = DateTimeOffset.FromFileTime((long)fspFileNode.FileInfo.CreationTime);
                var lastAccessTime = DateTimeOffset.FromFileTime((long)fspFileNode.FileInfo.LastAccessTime);
                var lastWriteTime = DateTimeOffset.FromFileTime((long)fspFileNode.FileInfo.LastWriteTime);
                var changeTime = DateTimeOffset.FromFileTime((long)fspFileNode.FileInfo.ChangeTime);
                var currentDirectory = string.IsNullOrWhiteSpace(fspFileNode.Path) ? new VfsEntry
                {
                    Name = ".",
                    Attributes = FileAttributes.Directory,
                    CreationTime = creationTime,
                    LastAccessTime = lastAccessTime,
                    LastWriteTime = lastWriteTime,
                    ChangeTime = changeTime,
                    Size = 0
                } : _projectionLayer.GetInfo(fspFileNode.Path);
                VfsEntry? parentDirectory = null;
                var parentDirectoryName = Path.GetDirectoryName(fspFileNode.Path);
                if (!string.IsNullOrWhiteSpace(fspFileNode.Path) &&
                    string.IsNullOrWhiteSpace(parentDirectoryName))
                {
                    parentDirectory = _projectionLayer.GetInfo(parentDirectoryName ?? string.Empty);
                }
                if (currentDirectory == null)
                {
                    throw new ArgumentNullException(nameof(currentDirectory));
                }
                if (parentDirectory == null)
                {
                    // This is the parent directory of the root.
                    parentDirectory = new VfsEntry
                    {
                        Name = "..",
                        Attributes = FileAttributes.Directory,
                        CreationTime = _rootTime,
                        LastAccessTime = _rootTime,
                        LastWriteTime = _rootTime,
                        ChangeTime = _rootTime,
                        Size = 0,
                    };
                }

                // Define the function that considers whether or not to emit an entry.
                bool HasHitMarker = false;
                bool EvaluateEntry(string name, VfsEntry entry)
                {
                    if (marker != null)
                    {
                        if (marker == name)
                        {
                            // We've hit the marker, skip it but then allow us to start
                            // emitting entries again.
                            HasHitMarker = true;
                            return true;
                        }
                        if (!HasHitMarker)
                        {
                            // Skip this entry, but continue.
                            return true;
                        }
                    }

                    if (pattern != null && !FileExpressionEvaluator.IsNameInExpression(pattern, name, true))
                    {
                        // Skip this entry, but continue.
                        return true;
                    }

#if ENABLE_TRACE_LOGS
                    _logger.LogTrace($"{guid}   Emit: {name}");
#endif

                    var bufferStillAvailable = callback(
                        name,
                        new FileInfo
                        {
                            FileAttributes = entry.IsDirectory ? (uint)FileAttributes.Directory : (uint)FileAttributes.Archive,
                            FileSize = (ulong)entry.Size,
                            AllocationSize = GetAllocationSize((ulong)entry.Size),
                            CreationTime = (ulong)entry.CreationTime.ToFileTime(),
                            ChangeTime = (ulong)entry.LastWriteTime.ToFileTime(),
                            LastAccessTime = (ulong)entry.LastWriteTime.ToFileTime(),
                            LastWriteTime = (ulong)entry.LastWriteTime.ToFileTime(),
                        });

#if ENABLE_TRACE_LOGS
                    if (!bufferStillAvailable)
                    {
                        _logger.LogTrace($"{guid}   Ending enumeration because buffer is now full.");
                    }
#endif

                    return bufferStillAvailable;
                }

                // Enumerate through the entries until the buffer is full.
                string? previousEntry = null;
                if (!EvaluateEntry(".", currentDirectory))
                {
                    return;
                }
                if (_enableCorrectnessChecks)
                {
                    previousEntry = ".";
                }
                if (!EvaluateEntry("..", parentDirectory))
                {
                    return;
                }
                if (_enableCorrectnessChecks)
                {
                    previousEntry = "..";
                }
                foreach (var entry in enumerable)
                {
                    if (_enableCorrectnessChecks)
                    {
                        if (_caseInsensitiveComparer.Compare(entry.Name, previousEntry) <= 0)
                        {
                            _logger.LogCritical($"Directory enumeration yielded '{entry.Name}' after '{previousEntry}'! Directory enumerations must be sorted!");
                        }
                    }

                    if (!EvaluateEntry(entry.Name, entry))
                    {
                        return;
                    }

                    if (_enableCorrectnessChecks)
                    {
                        previousEntry = entry.Name;
                    }
                }

                return;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        public override int GetDirInfoByName(
            object fileNode,
            object? fileDesc,
            string fileName,
            out string? normalizedName,
            out FileInfo fileInfo)
        {
            try
            {
                var fspFileNode = (WinFspFileNode)fileNode;
                normalizedName = null;
                fileInfo = default(FileInfo);

                // We can't perform this operation on non-directories.
                if (!fspFileNode.IsDirectory)
                {
                    return Trace(fspFileNode.Path, STATUS_NOT_A_DIRECTORY);
                }

                // Get the list of entries under this path.
                var entries = _projectionLayer.List(fspFileNode.Path);
                if (entries == null)
                {
                    return Trace(fspFileNode.Path, STATUS_OBJECT_PATH_NOT_FOUND);
                }

                // Find the first one that matches.
                var targetFileName = Path.GetFileName(fileName).ToLowerInvariant();
                var entry = entries.FirstOrDefault(x => x.Name.ToLowerInvariant() == targetFileName);
                if (entry == null)
                {
                    return Trace(fspFileNode.Path, STATUS_OBJECT_NAME_NOT_FOUND);
                }

                // Otherwise we found the entry.
                fileInfo = new FileInfo
                {
                    FileAttributes = entry.IsDirectory ? (uint)FileAttributes.Directory : (uint)FileAttributes.Archive,
                    FileSize = (ulong)entry.Size,
                    AllocationSize = GetAllocationSize((ulong)entry.Size),
                    CreationTime = (ulong)entry.CreationTime.ToFileTime(),
                    ChangeTime = (ulong)entry.LastWriteTime.ToFileTime(),
                    LastAccessTime = (ulong)entry.LastWriteTime.ToFileTime(),
                    LastWriteTime = (ulong)entry.LastWriteTime.ToFileTime(),
                };
                // normalizedName = Path.GetFileName(GetNormalizedName(fileName));
                normalizedName = GetNormalizedName(fileName);
                return Trace(fspFileNode.Path, STATUS_SUCCESS);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, ex.Message);
                throw;
            }
        }

        // GetReparsePointByName
        // GetReparsePoint
        // SetReparsePoint
        // DeleteReparsePoint
        // GetStreamEntry
        // GetEaEntry
        // SetEaEntry

    }
}