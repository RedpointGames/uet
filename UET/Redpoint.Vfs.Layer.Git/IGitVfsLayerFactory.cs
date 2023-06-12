namespace Redpoint.Vfs.Layer.Git
{
    /// <summary>
    /// A factory which creates Git virtual filesystem layer instances.
    /// </summary>
    public interface IGitVfsLayerFactory
    {
        /// <summary>
        /// Create a Git virtual filesystem layer for the bare Git repository at the specified path.
        /// </summary>
        /// <param name="barePath">The path to the bare Git repository on disk.</param>
        /// <param name="blobPath">When a file in the Git virtual filesystem layer is first accessed, it is materialised underneath this path. If left as null, materialised files are stored underneath <c>barePath/uefs-blob</c>.</param>
        /// <param name="indexCachePath">When the Git virtual filesystem layer is initialized, an indexed cache of the commit is created at this path. If left as null, materialised files are stored underneath <c>barePath/uefs-index-cache</c>.</param>
        /// <param name="commitHash">The commit to serve in this Git virtual filesysystem layer.</param>
        /// <returns>The new Git virtual filesystem layer.</returns>
        IGitVfsLayer CreateNativeLayer(
            string barePath,
            string? blobPath,
            string? indexCachePath,
            string commitHash);

        /// <summary>
        /// Creates a Git virtual filesystem layer for a GitHub repository.
        /// </summary>
        /// <param name="gitHubAccessToken">The GitHub access token to use.</param>
        /// <param name="owner">The owner of the GitHub repository.</param>
        /// <param name="repo">The GitHub repository.</param>
        /// <param name="blobPath">When a file in the Git virtual filesystem layer is first accessed, it is materialised underneath this path.</param>
        /// <param name="indexCachePath">When the Git virtual filesystem layer is initialized, an indexed cache of the commit is created at this path.</param>
        /// <param name="commitHash">The commit to serve in this Git virtual filesysystem layer.</param>
        /// <returns>The new Git virtual filesystem layer.</returns>
        IGitVfsLayer CreateGitHubLayer(
            string gitHubAccessToken,
            string owner,
            string repo,
            string blobPath,
            string indexCachePath,
            string commitHash);
    }
}
