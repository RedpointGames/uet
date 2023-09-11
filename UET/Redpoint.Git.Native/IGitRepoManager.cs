namespace Redpoint.Git.Native
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a local Git repository.
    /// </summary>
    public interface IGitRepoManager : IDisposable
    {
        /// <summary>
        /// Returns if the repository has the specified commit.
        /// </summary>
        /// <param name="commit">The commit hash to check.</param>
        /// <returns>True if the repository has the specified commmit.</returns>
        bool HasCommit(string commit);

        /// <summary>
        /// Fetches a commit from the specified SSH URL.
        /// </summary>
        /// <param name="url">The SSH URL.</param>
        /// <param name="commit">The commit to fetch.</param>
        /// <param name="publicKeyFile">The path to the public key file.</param>
        /// <param name="privateKeyFile">The path to the private key file.</param>
        /// <param name="onProgress">The callback when fetch progress is made.</param>
        /// <returns>An awaitable task that is complete when the fetch operation is complete.</returns>
        [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Git URLs can be of a form that is not compatible with the Uri object.")]
        Task Fetch(
            string url,
            string commit,
            string publicKeyFile,
            string privateKeyFile,
            Action<GitFetchProgressInfo> onProgress);

        /// <summary>
        /// Stops any Git processes that were launched by this instance if they are still running.
        /// </summary>
        void StopProcesses();
    }
}
