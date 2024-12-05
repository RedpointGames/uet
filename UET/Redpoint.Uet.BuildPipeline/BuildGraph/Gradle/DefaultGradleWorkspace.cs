namespace Redpoint.Uet.BuildPipeline.BuildGraph.Gradle
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using System;
    using System.Threading.Tasks;

    internal class DefaultGradleWorkspace : IGradleWorkspace
    {
        private readonly IDynamicWorkspaceProvider _dynamicWorkspaceProvider;
        private readonly ILogger<DefaultGradleWorkspace> _logger;

        public DefaultGradleWorkspace(
            IDynamicWorkspaceProvider dynamicWorkspaceProvider,
            ILogger<DefaultGradleWorkspace> logger)
        {
            _dynamicWorkspaceProvider = dynamicWorkspaceProvider;
            _logger = logger;
        }

        public async Task<GradleWorkspaceInstance> GetGradleWorkspaceInstance(CancellationToken cancellationToken)
        {
            IWorkspace? gradleDownloadCache = null;
            IWorkspace? gradleTemporaryWorkspace = null;

            var ok = false;
            try
            {
                gradleDownloadCache = await _dynamicWorkspaceProvider.GetWorkspaceAsync(new TemporaryWorkspaceDescriptor
                {
                    Name = "GradleDownloadCache",
                }, cancellationToken).ConfigureAwait(false);

                var gradleTemporaryWorkspaceAttempt = 0;
                do
                {
                    gradleTemporaryWorkspace = await _dynamicWorkspaceProvider.GetWorkspaceAsync(new TemporaryWorkspaceDescriptor
                    {
                        Name = $"GradleHome_{Environment.ProcessId}_{gradleTemporaryWorkspaceAttempt}",
                    }, cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation($"Checking the following Gradle home: {gradleTemporaryWorkspace.Path}");

                    // If the gradle home has any files in it, delete them. The gradle home must be wiped out before/after each
                    // build.
                    try
                    {
                        foreach (var entry in new DirectoryInfo(gradleTemporaryWorkspace.Path).GetFileSystemInfos())
                        {
                            _logger.LogInformation($"Deleting file/directory from previous Gradle home: {entry.FullName}");
                            if (entry is DirectoryInfo directory)
                            {
                                directory.Delete(true);
                            }
                            else
                            {
                                entry.Delete();
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                    catch
                    {
                        _logger.LogInformation($"Unable to use that Gradle home as one or more existing files/directories could not be removed.");
                        gradleTemporaryWorkspaceAttempt++;
                        await gradleTemporaryWorkspace.DisposeAsync().ConfigureAwait(false);
                        gradleTemporaryWorkspace = null;
                        cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }

                    // We have a Gradle home we can use.
                    break;
                } while (true);

                _logger.LogInformation($"Using the following Gradle home: {gradleTemporaryWorkspace.Path}");

                cancellationToken.ThrowIfCancellationRequested();

                var centralCacheSafeToUse = Path.Combine(gradleDownloadCache.Path, "safe-to-use");
                var centralCacheModules2 = Path.Combine(gradleDownloadCache.Path, "modules-2");
                var centralCacheWrapper = Path.Combine(gradleDownloadCache.Path, "wrapper");
                if (File.Exists(centralCacheSafeToUse) && Directory.Exists(centralCacheModules2))
                {
                    var homeCacheModules2 = Path.Combine(gradleTemporaryWorkspace.Path, "caches", "modules-2");
                    var homeCacheWrapper = Path.Combine(gradleTemporaryWorkspace.Path, "wrapper");

                    _logger.LogInformation($"Copying the existing Gradle download cache from '{centralCacheModules2}' to '{homeCacheModules2}': {gradleTemporaryWorkspace.Path}");
                    await DirectoryAsync.CopyAsync(centralCacheModules2, homeCacheModules2, true);

                    _logger.LogInformation($"Copying the existing Gradle wrapper from '{centralCacheWrapper}' to '{homeCacheWrapper}': {gradleTemporaryWorkspace.Path}");
                    await DirectoryAsync.CopyAsync(centralCacheWrapper, homeCacheWrapper, true);
                }

                cancellationToken.ThrowIfCancellationRequested();

                ok = true;
                return new GradleWorkspaceInstance(_logger, gradleDownloadCache, gradleTemporaryWorkspace);
            }
            finally
            {
                if (!ok)
                {
                    if (gradleDownloadCache != null)
                    {
                        try
                        {
                            await gradleDownloadCache.DisposeAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to release Gradle download cache: {ex}");
                        }
                    }

                    if (gradleTemporaryWorkspace != null)
                    {
                        try
                        {
                            await gradleTemporaryWorkspace.DisposeAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to release Gradle home: {ex}");
                        }
                    }
                }
            }
        }
    }
}
