namespace Redpoint.Vfs.Abstractions
{
    /// <summary>
    /// Represents a virtual filesystem entry.
    /// </summary>
    /// <remarks>
    /// We can't enforce immutability on this class without using properties, and we don't want to use properties because they're slower in performance critical code.
    /// </remarks>
    public record VfsEntry
    {
        /// <summary>
        /// The name of the virtual filesystem entry (i.e. the filename). This does not include the full path of the entry; it is only the name.
        /// </summary>
        public required string Name;

        /// <summary>
        /// The attributes of the virtual filesystem entry.
        /// </summary>
        public required FileAttributes Attributes;

        /// <summary>
        /// The time that the virtual filesystem entry was created.
        /// </summary>
        public required DateTimeOffset CreationTime;

        /// <summary>
        /// The time that the virtual filesystem entry was last accessed.
        /// </summary>
        public required DateTimeOffset LastAccessTime;

        /// <summary>
        /// The time that the contents of the virtual filesystem entry was last changed.
        /// </summary>
        public required DateTimeOffset LastWriteTime;

        /// <summary>
        /// THe time that the contents or metadata (such as attributes) of the virtual filesystem entry were last changed.
        /// </summary>
        public required DateTimeOffset ChangeTime;

        /// <summary>
        /// The length of the virtual filesystem entry content, if it is a file. If this is not a file, this value will be 0.
        /// </summary>
        public required long Size;

        /// <summary>
        /// True if the virtual filesystem entry is a directory.
        /// </summary>
        public bool IsDirectory => (Attributes & FileAttributes.Directory) != 0;
    }
}
