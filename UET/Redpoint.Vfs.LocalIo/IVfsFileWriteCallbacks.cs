namespace Redpoint.Vfs.LocalIo
{
    /// <summary>
    /// Optional callbacks when operations are performed against local I/O file handles.
    /// </summary>
    public interface IVfsFileWriteCallbacks
    {
        /// <summary>
        /// When a write is performed against a local file, this is called. Used by the scratch layer to clear out projection layer caches.
        /// </summary>
        /// <param name="scratchPath">The scratch path that was provided in the <see cref="ILocalIoVfsFileFactory.CreateVfsFileHandle(string, FileMode, FileAccess, FileShare, IVfsFileWriteCallbacks?, string?)"/> call.</param>
        void OnObjectModifiedAtRelativePath(string scratchPath);
    }
}
