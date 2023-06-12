namespace Redpoint.Vfs.Abstractions
{
    /// <summary>
    /// The existence status for a virtual filesystem entry.
    /// </summary>
    public enum VfsEntryExistence
    {
        /// <summary>
        /// The virtual filesystem entry does not exist.
        /// </summary>
        DoesNotExist,

        /// <summary>
        /// The virtual filesystem entry is a directory.
        /// </summary>
        DirectoryExists,

        /// <summary>
        /// The virtual filesystem entry is an ordinary file.
        /// </summary>
        FileExists,
    }
}
