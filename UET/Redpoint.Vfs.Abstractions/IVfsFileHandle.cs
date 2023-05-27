namespace Redpoint.Vfs.Abstractions
{
    public interface IVfsFileHandle<out T> : IDisposable where T : IVfsFile
    {
        T VfsFile { get; }
    }
}
