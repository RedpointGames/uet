namespace Redpoint.Git.Abstractions
{
    using Redpoint.Vfs.Abstractions;

    /// <summary>
    /// Represents an entry in a Git tree.
    /// </summary>
    public record GitVfsEntry : VfsEntry
    {
        /// <summary>
        /// The SHA hash of the blob, if this is a file and not a directory.
        /// </summary>
        public required string? BlobSha { get; init; }

        /// <summary>
        /// The absolute path of the parent directory that contains this entry.
        /// </summary>
        public required string AbsoluteParentPath { get; init; }

        /// <summary>
        /// The absolute path of the entry.
        /// </summary>
        public required string AbsolutePath { get; init; }
    }
}
