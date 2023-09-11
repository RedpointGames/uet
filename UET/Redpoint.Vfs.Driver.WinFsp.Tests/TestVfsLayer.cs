namespace Redpoint.Vfs.Driver.WinFsp.Tests
{
    using Redpoint.Vfs.Abstractions;
    using System.Collections.Generic;

    internal class TestVfsLayer : IVfsLayer
    {
        public bool ReadOnly => true;

        public bool CreateDirectory(string path)
        {
            return false;
        }

        public bool DeleteDirectory(string path)
        {
            return false;
        }

        public bool DeleteFile(string path)
        {
            return false;
        }

        public void Dispose()
        {
        }

        public VfsEntryExistence Exists(string path)
        {
            return VfsEntryExistence.DoesNotExist;
        }

        public VfsEntry? GetInfo(string path)
        {
            return null;
        }

        public IEnumerable<VfsEntry>? List(string path)
        {
            return Array.Empty<VfsEntry>();
        }

        public bool MoveFile(string oldPath, string newPath, bool replace)
        {
            return false;
        }

        public IVfsFileHandle<IVfsFile>? OpenFile(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, ref VfsEntry? metadata)
        {
            return null;
        }

        public bool SetBasicInfo(string path, uint? attributes, DateTimeOffset? creationTime, DateTimeOffset? lastAccessTime, DateTimeOffset? lastWriteTime, DateTimeOffset? changeTime)
        {
            return false;
        }
    }
}