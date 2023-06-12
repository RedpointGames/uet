namespace Redpoint.Vfs.Abstractions
{
    /// <summary>
    /// Represents a virtual filesystem layer, which provides content that a virtual filesystem driver can serve.
    /// </summary>
    public interface IVfsLayer : IDisposable
    {
        /// <summary>
        /// List the virtual filesystem entries at the specified path. To fetch the entries at the root of the virtual filesystem layer, provide an empty string.
        /// </summary>
        /// <param name="path">The path under which to list entries. Provide an empty string to get the entries at the root of the virtual filesystem layer.</param>
        /// <returns>The enumeration of virtual filesystem entries.</returns>
        IEnumerable<VfsEntry>? List(string path);

        /// <summary>
        /// Returns whether a virtual filesystem entry exists as the specified path, and if so, whether it is a file or directory.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>The existence status of the path.</returns>
        VfsEntryExistence Exists(string path);

        /// <summary>
        /// Returns information about the virtual filesystem entry at the specified path, or <c>null</c> if no entry exists.
        /// </summary>
        /// <param name="path">The path to the virtual filesystem entry.</param>
        /// <returns>The virtual filesystem entry.</returns>
        VfsEntry? GetInfo(string path);

        /// <summary>
        /// Opens a handle to the virtual filesystem file. The handle must be disposed when no longer in use.
        /// </summary>
        /// <param name="path">The path to the virtual filesystem entry.</param>
        /// <param name="fileMode">The file mode for opening.</param>
        /// <param name="fileAccess">The file access for opening.</param>
        /// <param name="fileShare">The file share for opening.</param>
        /// <param name="metadata">The metadata about the file that was opened if successful (this is the same information that would be returned by <see cref="GetInfo(string)"/>).</param>
        /// <returns>The opened handle, or null if the file could not be opened.</returns>
        IVfsFileHandle<IVfsFile>? OpenFile(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, ref VfsEntry? metadata);

        /// <summary>
        /// Creates a directory in the virtual filesystem layer at the specified path.
        /// </summary>
        /// <param name="path">The path to the directory to create.</param>
        /// <returns>True if the directory was created or already exists.</returns>
        bool CreateDirectory(string path);

        /// <summary>
        /// Moves a file or directory on the virtual filesystem layer to a new path.
        /// </summary>
        /// <param name="oldPath">The current path of the virtual filesystem entry.</param>
        /// <param name="newPath">The new path of the virtual filesystem entry.</param>
        /// <param name="replace">If true, the existing file should be replaced.</param>
        /// <returns>True if the move operation succeeded.</returns>
        bool MoveFile(string oldPath, string newPath, bool replace);

        /// <summary>
        /// Deletes the file in the virtual filesystem layer at the specified path.
        /// </summary>
        /// <param name="path">The path to the virtual filesystem file.</param>
        /// <returns>True if the delete operation succeeded.</returns>
        bool DeleteFile(string path);

        /// <summary>
        /// Deletes the directory in the virtual filesystem layer at the specified path.
        /// </summary>
        /// <param name="path">The path to the virtual filesystem directory.</param>
        /// <returns>True if the delete operation succeeded.</returns>
        bool DeleteDirectory(string path);

        /// <summary>
        /// Sets the metadata for the virtual filesystem entry.
        /// </summary>
        /// <param name="path">The path to set the metadata for.</param>
        /// <param name="attributes">The new attributes for the entry, or null if the attributes should not be updated.</param>
        /// <param name="creationTime">The new creation time for the entry, or null if the creation time should not be updated.</param>
        /// <param name="lastAccessTime">The new access time for the entry, or null if the access time should not be updated.</param>
        /// <param name="lastWriteTime">The new write time for the entry, or null if the write time should not be updated.</param>
        /// <param name="changeTime">The new change time for the entry, or null if the change time should not be updated.</param>
        /// <returns>True if updating the metadata succeeded.</returns>
        bool SetBasicInfo(
            string path,
            uint? attributes,
            DateTimeOffset? creationTime,
            DateTimeOffset? lastAccessTime,
            DateTimeOffset? lastWriteTime,
            DateTimeOffset? changeTime);

        /// <summary>
        /// If true, this virtual filesystem layer is read-only and all read-write operations will fail.
        /// </summary>
        bool ReadOnly { get; }
    }
}
