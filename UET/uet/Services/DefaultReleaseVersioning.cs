namespace UET.Services
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Uet.Configuration.Plugin;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Text.Json;
    using UET.Commands.Build;
    using UET.Commands.EngineSpec;

    internal sealed class DefaultReleaseVersioning : IReleaseVersioning
    {
        private readonly ILogger<DefaultReleaseVersioning> _logger;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;

        public DefaultReleaseVersioning(
            ILogger<DefaultReleaseVersioning> logger,
            IEngineWorkspaceProvider engineWorkspaceProvider)
        {
            _logger = logger;
            _engineWorkspaceProvider = engineWorkspaceProvider;
        }

        private static EngineBuildVersionJson? GetEngineVersionInfo(string path)
        {
            var buildVersion = Path.Combine(path, "Engine", "Build", "Build.version");
            if (File.Exists(buildVersion))
            {
                using (var stream = new FileStream(buildVersion, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return JsonSerializer.Deserialize(stream, SourceGenerationContext.Default.EngineBuildVersionJson);
                }
            }
            return null;
        }

        public async Task<(string versionName, string versionNumber)> ComputePluginVersionNameAndNumberAsync(
            BuildEngineSpecification engineSpec,
            BuildConfigPluginPackageType pluginVersioningType,
            CancellationToken cancellationToken)
        {
            var ciCommitShortSha = Environment.GetEnvironmentVariable("CI_COMMIT_SHORT_SHA");
            var overrideDateVersion = Environment.GetEnvironmentVariable("OVERRIDE_DATE_VERSION");
            if (!string.IsNullOrWhiteSpace(ciCommitShortSha) &&
                ciCommitShortSha.Length >= 8)
            {
                var currentTime = DateTimeOffset.UtcNow;
                var unixTimestamp = currentTime.ToUnixTimeSeconds();
                var versionDateTime = currentTime.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(overrideDateVersion))
                {
                    versionDateTime = overrideDateVersion;
                }

                EngineBuildVersionJson? engineInfo;
                await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                    engineSpec,
                    string.Empty,
                    cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var engineWorkspace).ConfigureAwait(false))
                {
                    engineInfo = GetEngineVersionInfo(engineWorkspace.Path);
                    if (engineInfo == null)
                    {
                        throw new BuildMisconfigurationException("Specified engine does not have a build.version file, but having version information is required in order to build and package plugins.");
                    }
                }

                var versionNumber = pluginVersioningType switch
                {
                    BuildConfigPluginPackageType.None => $"{unixTimestamp}{engineInfo.MinorVersion}",
                    BuildConfigPluginPackageType.Generic => $"{unixTimestamp}{engineInfo.MinorVersion}",
                    BuildConfigPluginPackageType.Marketplace => $"{unixTimestamp}{engineInfo.MinorVersion}",
                    BuildConfigPluginPackageType.Fab => $"{unixTimestamp}{engineInfo.MinorVersion}",
                    _ => throw new NotSupportedException("The value of 'BuildConfigPluginPackageType' is not supported in ComputeVersionNameAndNumberAsync."),
                };
                var versionName = pluginVersioningType switch
                {
                    BuildConfigPluginPackageType.None => $"{versionDateTime}-{engineInfo.MajorVersion}.{engineInfo.MinorVersion}-{ciCommitShortSha[..8]}",
                    BuildConfigPluginPackageType.Generic => $"{versionDateTime}-{engineInfo.MajorVersion}.{engineInfo.MinorVersion}-{ciCommitShortSha[..8]}",
                    BuildConfigPluginPackageType.Marketplace => $"{versionDateTime}-{engineInfo.MajorVersion}.{engineInfo.MinorVersion}-{ciCommitShortSha[..8]}",
                    BuildConfigPluginPackageType.Fab => $"{versionDateTime}-{engineInfo.MajorVersion}.{engineInfo.MinorVersion}-{ciCommitShortSha[..8]}",
                    _ => throw new NotSupportedException("The value of 'BuildConfigPluginPackageType' is not supported in ComputeVersionNameAndNumberAsync."),
                };

                _logger.LogInformation($"Building as versioned package: {versionName}");
                return (versionName, versionNumber);
            }
            else
            {
                _logger.LogInformation($"Building as unversioned package");
                return ("Unversioned", "10000");
            }
        }

        [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Not used for security.")]
        public string ComputeProjectReleaseVersion()
        {
            var overrideDateVersion = Environment.GetEnvironmentVariable("OVERRIDE_DATE_VERSION");
            var currentTime = DateTimeOffset.UtcNow;
            var unixTimestamp = currentTime.ToUnixTimeSeconds();
            var versionDateTime = currentTime.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(overrideDateVersion))
            {
                versionDateTime = overrideDateVersion;
            }

            string versionName;

            var ciCommitShortSha = Environment.GetEnvironmentVariable("CI_COMMIT_SHORT_SHA");
            if (!string.IsNullOrWhiteSpace(ciCommitShortSha) &&
                ciCommitShortSha.Length >= 8)
            {
                versionName = $"{versionDateTime}-{ciCommitShortSha[..8]}";
            }
            else
            {
                versionName = $"{versionDateTime}-dev-{unixTimestamp}";
            }

            _logger.LogInformation($"Building as versioned project: {versionName}");
            return versionName;
        }
    }
}
