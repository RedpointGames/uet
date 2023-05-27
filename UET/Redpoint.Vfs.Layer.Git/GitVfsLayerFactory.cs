namespace Redpoint.Vfs.Layer.Git
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Octokit;
    using Redpoint.Git.GitHub;
    using Redpoint.Git.Native;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.LocalIo;

    internal class GitVfsLayerFactory : IGitVfsLayerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public GitVfsLayerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private static GitHubClient GetClientWithToken(string token)
        {
            var client = new GitHubClient(new ProductHeaderValue("uefs.redpoint.games"));
            client.Credentials = new Octokit.Credentials(token);
            return client;
        }

        public IGitVfsLayer CreateGitHubLayer(
            string gitHubAccessToken,
            string owner,
            string repo,
            string blobPath,
            string indexCachePath,
            string commitHash)
        {
            return new GitVfsLayer(
                _serviceProvider.GetRequiredService<ILogger<GitVfsLayer>>(),
                _serviceProvider.GetRequiredService<ILocalIoVfsFileFactory>(),
                new GitHubGitRepository(GetClientWithToken(gitHubAccessToken), owner, repo),
                blobPath,
                indexCachePath,
                commitHash);
        }

        public IGitVfsLayer CreateNativeLayer(
            string barePath,
            string? blobPath,
            string? indexCachePath,
            string commitHash)
        {
            return new GitVfsLayer(
                _serviceProvider.GetRequiredService<ILogger<GitVfsLayer>>(),
                _serviceProvider.GetRequiredService<ILocalIoVfsFileFactory>(),
                new NativeGitRepository(new LibGit2Sharp.Repository(barePath)),
                blobPath ?? Path.Combine(barePath, "uefs-blob"),
                indexCachePath ?? Path.Combine(barePath, "uefs-index-cache"),
                commitHash);
        }
    }
}
