namespace Redpoint.Vfs.Abstractions
{
    public interface IVfsLayer : IDisposable
    {
        IEnumerable<VfsEntry>? List(string path);

        VfsEntryExistence Exists(string path);

        VfsEntry? GetInfo(string path);

        IVfsFileHandle<IVfsFile>? OpenFile(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, ref VfsEntry? metadata);

        bool CreateDirectory(string path);

        bool MoveFile(string oldPath, string newPath, bool replace);

        bool DeleteFile(string path);

        bool DeleteDirectory(string path);

        bool SetBasicInfo(
            string path,
            uint? attributes,
            DateTimeOffset? creationTime,
            DateTimeOffset? lastAccessTime,
            DateTimeOffset? lastWriteTime,
            DateTimeOffset? changeTime);

        bool ReadOnly { get; }
    }
}
