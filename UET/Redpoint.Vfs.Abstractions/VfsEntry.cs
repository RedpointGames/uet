namespace Redpoint.Vfs.Abstractions
{
    // @note: We can't enforce immutability on this class
    // without using properties, and we don't want to use
    // properties because they're slower in performance
    // critical code.
    public record VfsEntry
    {
        public required string Name;
        public required FileAttributes Attributes;
        public required DateTimeOffset CreationTime;
        public required DateTimeOffset LastAccessTime;
        public required DateTimeOffset LastWriteTime;
        public required DateTimeOffset ChangeTime;
        public required long Size;
        public bool IsDirectory => (Attributes & FileAttributes.Directory) != 0;
    }
}
