namespace Redpoint.Git.Abstractions
{
    /// <summary>
    /// Represents a Git repository.
    /// </summary>
    public interface IGitRepository : IDisposable
    {
        /// <summary>
        /// Resolves a ref (such as a branch or tag name) to a Git commit hash.
        /// </summary>
        /// <param name="ref">The branch or tag name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task that returns the Git commit hash, or <c>null</c> if it can't be resolved.</returns>
        Task<string?> ResolveRefToShaAsync(string @ref, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the Git commit for the given commit hash.
        /// </summary>
        /// <param name="sha">The commit hash.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task that returns the <see cref="IGitCommit"/>.</returns>
        Task<IGitCommit> GetCommitByShaAsync(string sha, CancellationToken cancellationToken);

        /// <summary>
        /// Materializes the content of a Git blob to disk.
        /// </summary>
        /// <param name="sha">The SHA hash of the Git blob to materialize.</param>
        /// <param name="destinationPath">The location at which the blob should be materialized.</param>
        /// <param name="contentAdjust">The callback to adjust the blob content before it's saved to disk.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task that returns the length of the materialized content.</returns>
        Task<long> MaterializeBlobToDiskByShaAsync(string sha, string destinationPath, Func<string, string>? contentAdjust, CancellationToken cancellationToken);
    }
}
