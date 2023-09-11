namespace Redpoint.Vfs.Driver.WinFsp
{
    using Redpoint.Vfs.Abstractions;
    using System;
    using System.IO;
    using System.Runtime.Versioning;
    using FileInfo = Fsp.Interop.FileInfo;

    [SupportedOSPlatform("windows6.2")]
    internal class WinFspFileNode : IDisposable
    {
        public static WinFspFileNode AsNewDirectoryWithPath(
            string path,
            DateTimeOffset createdTime)
        {
            return new WinFspFileNode(
                path,
                new FileInfo
                {
                    FileAttributes = (uint)FileAttributes.Directory,
                    CreationTime = (ulong)createdTime.ToFileTime(),
                    ChangeTime = (ulong)createdTime.ToFileTime(),
                    LastAccessTime = (ulong)createdTime.ToFileTime(),
                    LastWriteTime = (ulong)createdTime.ToFileTime(),
                    FileSize = 0,
                    AllocationSize = 0,
                    // @note: If we see weird behaviour with ISPC, we might need to
                    // make this "correct".
                    IndexNumber = (ulong)path.GetHashCode(StringComparison.OrdinalIgnoreCase),
                },
                null);
        }

        public static WinFspFileNode AsExistingDirectoryWithPath(
            string path,
            VfsEntry metadata)
        {
            return new WinFspFileNode(
                path,
                new FileInfo
                {
                    FileAttributes = (uint)metadata.Attributes,
                    CreationTime = (ulong)metadata.CreationTime.ToFileTime(),
                    ChangeTime = (ulong)metadata.ChangeTime.ToFileTime(),
                    LastAccessTime = (ulong)metadata.LastAccessTime.ToFileTime(),
                    LastWriteTime = (ulong)metadata.LastWriteTime.ToFileTime(),
                    FileSize = 0,
                    AllocationSize = 0,
                    IndexNumber = (ulong)path.GetHashCode(StringComparison.OrdinalIgnoreCase),
                },
                null);
        }

        public static WinFspFileNode AsFileWithPath(
            string path,
            IVfsFileHandle<IVfsFile> handle,
            VfsEntry metadata)
        {
            return new WinFspFileNode(
                path,
                new FileInfo
                {
                    FileAttributes = (uint)FileAttributes.Archive,
                    CreationTime = (ulong)metadata.CreationTime.ToFileTime(),
                    ChangeTime = (ulong)metadata.ChangeTime.ToFileTime(),
                    LastAccessTime = (ulong)metadata.LastAccessTime.ToFileTime(),
                    LastWriteTime = (ulong)metadata.LastWriteTime.ToFileTime(),
                    FileSize = (ulong)metadata.Size,
                    AllocationSize = WinFspVfsDriverImpl.GetAllocationSize((ulong)metadata.Size),
                    IndexNumber = (ulong)path.GetHashCode(StringComparison.OrdinalIgnoreCase),
                },
                handle);
        }

        private WinFspFileNode(
            string path,
            FileInfo fileInfo,
            IVfsFileHandle<IVfsFile>? projectedFileHandle)
        {
            Path = path;
            FileInfo = fileInfo;
            ProjectedFileHandle = projectedFileHandle;
        }

        public void Dispose()
        {
            ProjectedFileHandle?.Dispose();
            ProjectedFileHandle = null;
        }

        public string Path { get; }

        public bool IsDirectory => (FileInfo.FileAttributes & (uint)FileAttributes.Directory) != 0;

        public FileInfo FileInfo;

        public IVfsFileHandle<IVfsFile>? ProjectedFileHandle { get; private set; }
    }
}