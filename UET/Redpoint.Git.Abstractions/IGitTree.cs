namespace Redpoint.Git.Abstractions
{
    using Redpoint.Vfs.Abstractions;

    /// <summary>
    /// Represents a Git tree.
    /// </summary>
    public interface IGitTree
    {
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

        /// <summary>
        /// Represents metrics of a recursive enumeration.
        /// </summary>
        public class GitTreeEnumerationMetrics
        {
            private readonly Action<long> _onObjectsMappedUpdated;
            private long _objectsMapped = 0;

            /// <summary>
            /// Constructs a new <see cref="GitTreeEnumerationMetrics"/> for tracking enumeration metrics.
            /// </summary>
            /// <param name="onObjectsMappedUpdated">The callback to fire when the number of objects mapped changes.</param>
            public GitTreeEnumerationMetrics(Action<long> onObjectsMappedUpdated)
            {
                _onObjectsMappedUpdated = onObjectsMappedUpdated;
            }

            /// <summary>
            /// The number of objects mapped in the recursive enumeration.
            /// </summary>
            public long ObjectsMapped
            {
                get => _objectsMapped;
                set
                {
                    _objectsMapped = value;
                    _onObjectsMappedUpdated(_objectsMapped);
                }
            }
        }

        /// <summary>
        /// The SHA hash of the tree.
        /// </summary>
        string Sha { get; }

        /// <summary>
        /// Recursively enumerate through the Git tree.
        /// </summary>
        /// <param name="metrics">The metrics to report the progress of the enumeration to.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The asynchronous enumerable that returns <see cref="GitVfsEntry"/> entries.</returns>
        IAsyncEnumerable<GitVfsEntry> EnumerateRecursivelyAsync(GitTreeEnumerationMetrics? metrics, CancellationToken cancellationToken);
    }
}
