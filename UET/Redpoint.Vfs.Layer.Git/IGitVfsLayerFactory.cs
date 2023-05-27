namespace Redpoint.Vfs.Layer.Git
{
    using Redpoint.Vfs.Abstractions;

    public interface IGitVfsLayerFactory
    {
        IGitVfsLayer CreateNativeLayer(
            string barePath,
            string? blobPath,
            string? indexCachePath,
            string commitHash);

        IGitVfsLayer CreateGitHubLayer(
            string gitHubAccessToken,
            string owner,
            string repo,
            string blobPath,
            string indexCachePath,
            string commitHash);
    }
}
