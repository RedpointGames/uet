namespace Redpoint.Vfs.LocalIo
{
    using Redpoint.Vfs.Abstractions;

    /// <summary>
    /// Provides a factory which constructs <see cref="IVfsFileHandle{IVfsFile}"/> instances.
    /// </summary>
    public interface ILocalIoVfsFileFactory
    {
        /// <summary>
        /// Creates a virtual filesystem file handle that acts as a handle for a file on the local system.
        /// </summary>
        /// <param name="path">The path on the local machine.</param>
        /// <param name="fileMode">The file mode to open the file with.</param>
        /// <param name="fileAccess">The file access to open the file with.</param>
        /// <param name="fileShare">The file share to open the file with.</param>
        /// <param name="callbacks">Optional callbacks that are fired when write operations are performed against the file. Used by the read-write scratch layer to indicate when a file receives writes to it's content.</param>
        /// <param name="scratchPath">The optional scratch file path when the callbacks are made. If <paramref name="callbacks"/> is not null, this value must be non-null as well.</param>
        /// <returns>The virtual filesystem file handle.</returns>
        IVfsFileHandle<IVfsFile> CreateVfsFileHandle(
            string path,
            FileMode fileMode,
            FileAccess fileAccess,
            FileShare fileShare,
            IVfsFileWriteCallbacks? callbacks,
            string? scratchPath);

        /// <summary>
        /// Creates a virtual filesystem file handle that represents a portion of a file that exists on disk.
        /// </summary>
        /// <param name="path">The path on the local machine.</param>
        /// <param name="offset">The offset of the file on disk; this will appear as position 0 in the returned virtual filesystem file handle.</param>
        /// <param name="length">The length of the projected area; this will appear as the length in the returned virtual filesystem file handle.</param>
        /// <returns>The virtual filesystem file handle.</returns>
        IVfsFileHandle<IVfsFile> CreateOffsetVfsFileHandle(
            string path,
            long offset,
            long length);
    }
}
