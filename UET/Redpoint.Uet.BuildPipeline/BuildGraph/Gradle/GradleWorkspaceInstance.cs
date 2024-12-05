namespace Redpoint.Uet.BuildPipeline.BuildGraph.Gradle
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.Uet.Workspace;
    using System;
    using System.Threading.Tasks;

    internal class GradleWorkspaceInstance : IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly IWorkspace _gradleDownloadCache;
        private readonly IWorkspace _gradleTemporaryWorkspace;
        private bool _buildSuccessful;

        public GradleWorkspaceInstance(
            ILogger logger,
            IWorkspace gradleDownloadCache,
            IWorkspace gradleTemporaryWorkspace)
        {
            _logger = logger;
            _gradleDownloadCache = gradleDownloadCache;
            _gradleTemporaryWorkspace = gradleTemporaryWorkspace;
            _buildSuccessful = false;
        }

        public string GradleHomePath => _gradleTemporaryWorkspace.Path;

        public void MarkBuildAsSuccessful()
        {
            _buildSuccessful = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_buildSuccessful)
            {
                // If the build was successful, copy the Gradle download cache back.
                var centralCacheSafeToUse = Path.Combine(_gradleDownloadCache.Path, "safe-to-use");
                var centralCacheModules2 = Path.Combine(_gradleDownloadCache.Path, "modules-2");
                var centralCacheWrapper = Path.Combine(_gradleDownloadCache.Path, "wrapper");

                var homeCacheModules2 = Path.Combine(_gradleTemporaryWorkspace.Path, "caches", "modules-2");
                var homeCacheWrapper = Path.Combine(_gradleTemporaryWorkspace.Path, "wrapper");

                try
                {
                    _logger.LogInformation($"Removing existing download cache so we can copy across the new version from the build: {_gradleDownloadCache.Path}");
                    if (File.Exists(centralCacheSafeToUse))
                    {
                        File.Delete(centralCacheSafeToUse);
                    }

                    do
                    {
                        try
                        {
                            if (Directory.Exists(homeCacheModules2))
                            {
                                await DirectoryAsync.DeleteAsync(centralCacheModules2, true);
                            }
                            if (Directory.Exists(homeCacheWrapper))
                            {
                                await DirectoryAsync.DeleteAsync(centralCacheWrapper, true);
                            }
                            break;
                        }
                        catch (IOException ex) when (ex.Message.Contains("The directory is not empty.", StringComparison.Ordinal))
                        {
                            _logger.LogInformation("Can't remove existing central cache yet, trying again in 1 second...");
                            await Task.Delay(1000);
                            continue;
                        }
                    }
                    while (true);

                    if (Directory.Exists(homeCacheModules2))
                    {
                        _logger.LogInformation($"Copying home Gradle download cache from '{homeCacheModules2}' to central download cache at '{centralCacheModules2}'.");
                        await DirectoryAsync.CopyAsync(homeCacheModules2, centralCacheModules2, true);
                    }

                    if (Directory.Exists(homeCacheWrapper))
                    {
                        _logger.LogInformation($"Copying home Gradle wrapper from '{homeCacheWrapper}' to central download cache at '{centralCacheWrapper}'.");
                        await DirectoryAsync.CopyAsync(homeCacheWrapper, centralCacheWrapper, true);
                    }

                    _logger.LogInformation($"Marking central download cache as safe to use.");
                    File.WriteAllText(centralCacheSafeToUse, "ok");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to copy download cache back to central download cache; it will not be used until a successful copy occurs again: {ex}");
                }
            }
            else
            {
                _logger.LogInformation($"Not syncing download cache to central download cache as the build was not successful.");
            }

            try
            {
                await _gradleDownloadCache.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to release Gradle download cache: {ex}");
            }

            try
            {
                await _gradleTemporaryWorkspace.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to release Gradle home: {ex}");
            }
        }
    }
}
