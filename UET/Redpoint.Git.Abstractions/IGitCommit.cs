namespace Redpoint.Git.Abstractions
{
    /// <summary>
    /// Represents a Git commit.
    /// </summary>
    public interface IGitCommit
    {
        /// <summary>
        /// The date when the commit was committed.
        /// </summary>
        DateTimeOffset CommittedAtUtc { get; }

        /// <summary>
        /// Retrieves the root <see cref="IGitTree"/> for the commit.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task that returns the <see cref="IGitTree"/>.</returns>
        Task<IGitTree> GetRootTreeAsync(CancellationToken cancellationToken);
    }
}
