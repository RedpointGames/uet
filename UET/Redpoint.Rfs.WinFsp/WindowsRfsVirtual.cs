namespace Redpoint.Rfs.WinFsp
{
    using Fsp.Interop;
    using System.IO;
    using System.Runtime.Versioning;
    using static Grpc.Core.Metadata;
    using FspFileInfo = Fsp.Interop.FileInfo;

    [SupportedOSPlatform("windows6.2")]
    internal static class WindowsRfsVirtual
    {
        public static FspFileInfo GetVirtualDirectoryOnHost(ulong creationTime, ulong changeTime, ulong lastAccessTime, ulong lastWriteTime)
        {
            return new FspFileInfo
            {
                FileAttributes = (uint)FileAttributes.Directory,
                FileSize = 0,
                AllocationSize = 0,
                CreationTime = creationTime,
                ChangeTime = changeTime,
                LastAccessTime = lastAccessTime,
                LastWriteTime = lastWriteTime,
            };
        }

        public static FspFileInfo GetVirtualJunctionOnHost(ulong creationTime, ulong changeTime, ulong lastAccessTime, ulong lastWriteTime)
        {
            return new FspFileInfo
            {
                FileAttributes = (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory),
                FileSize = 0,
                AllocationSize = 0,
                CreationTime = creationTime,
                ChangeTime = changeTime,
                LastAccessTime = lastAccessTime,
                LastWriteTime = lastWriteTime,
            };
        }

        public static FspFileInfo GetVirtualDirectoryOnClient(string path)
        {
            WindowsFileDesc.GetFileInfoFromFileSystemInfo(
                new DirectoryInfo(path),
                out var fileInfo);
            return new FspFileInfo
            {
                FileAttributes = (uint)FileAttributes.Directory,
                FileSize = 0,
                AllocationSize = 0,
                CreationTime = fileInfo.CreationTime,
                ChangeTime = fileInfo.ChangeTime,
                LastAccessTime = fileInfo.LastAccessTime,
                LastWriteTime = fileInfo.LastWriteTime,
            };
        }

        public static FspFileInfo GetVirtualJunctionOnClient(string path)
        {
            WindowsFileDesc.GetFileInfoFromFileSystemInfo(
                new DirectoryInfo(path),
                out var fileInfo);
            return new FspFileInfo
            {
                FileAttributes = (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory),
                FileSize = 0,
                AllocationSize = 0,
                CreationTime = fileInfo.CreationTime,
                ChangeTime = fileInfo.ChangeTime,
                LastAccessTime = fileInfo.LastAccessTime,
                LastWriteTime = fileInfo.LastWriteTime,
            };
        }
    }
}
