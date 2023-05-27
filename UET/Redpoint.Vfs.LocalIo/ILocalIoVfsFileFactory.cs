namespace Redpoint.Vfs.LocalIo
{
    using Redpoint.Vfs.Abstractions;

    public interface ILocalIoVfsFileFactory
    {
        IVfsFileHandle<IVfsFile> CreateVfsFileHandle(
            string path,
            FileMode fileMode,
            FileAccess fileAccess,
            FileShare fileShare,
            IWindowsVfsFileCallbacks? callbacks,
            string? scratchPath);

        IVfsFileHandle<IVfsFile> CreateOffsetVfsFileHandle(
            string path,
            long offset,
            long length);
    }
}
