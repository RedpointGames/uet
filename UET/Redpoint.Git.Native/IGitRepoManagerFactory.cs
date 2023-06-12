namespace Redpoint.Git.Native
{
    /// <summary>
    /// Creates instances of <see cref="IGitRepoManager" />.
    /// </summary>
    public interface IGitRepoManagerFactory
    {
        /// <summary>
        /// Creates an instance of <see cref="IGitRepoManager"/> for the specified <paramref name="gitRepoPath"/>.
        /// </summary>
        /// <param name="gitRepoPath">The Git repository path.</param>
        /// <returns>The new <see cref="IGitRepoManager"/> instance.</returns>
        IGitRepoManager CreateGitRepoManager(string gitRepoPath);
    }
}
