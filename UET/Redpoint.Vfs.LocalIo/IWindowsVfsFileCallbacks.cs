namespace Redpoint.Vfs.LocalIo
{
    public interface IWindowsVfsFileCallbacks
    {
        void OnObjectModifiedAtRelativePath(string relativePath);
    }
}
