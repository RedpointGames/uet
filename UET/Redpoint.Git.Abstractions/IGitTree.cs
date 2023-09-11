namespace Redpoint.Git.Abstractions
{
    /// <summary>
    /// Represents a Git tree.
    /// </summary>
    public interface IGitTree
    {
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
