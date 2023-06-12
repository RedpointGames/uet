namespace Redpoint.Git.Native
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a local Git repository.
    /// </summary>
    public interface IGitRepoManager
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
