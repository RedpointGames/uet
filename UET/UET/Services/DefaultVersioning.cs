namespace UET.Services
{
    using Microsoft.Extensions.Logging;
    using Redpoint.UET.BuildPipeline.Executors;
    using Redpoint.UET.BuildPipeline.Executors.Engine;
    using System;
    using System.Text.Json;
    using UET.Commands.Build;
    using UET.Commands.EngineSpec;

    internal class DefaultVersioning : IVersioning
    {
        private readonly ILogger<DefaultVersioning> _logger;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;

        public DefaultVersioning(
            ILogger<DefaultVersioning> logger,
            IEngineWorkspaceProvider engineWorkspaceProvider)
        {
            _logger = logger;
            _engineWorkspaceProvider = engineWorkspaceProvider;
        }

        private EngineBuildVersionJson? GetEngineVersionInfo(string path)
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

        public async Task<(string versionName, string versionNumber)> ComputeVersionNameAndNumberAsync(
            BuildEngineSpecification engineSpec,
            bool useStorageVirtualisation,
            CancellationToken cancellationToken)
        {
            var ciCommitShortSha = Environment.GetEnvironmentVariable("CI_COMMIT_SHORT_SHA");
            var overrideDateVersion = Environment.GetEnvironmentVariable("OVERRIDE_DATE_VERSION");
            if (!string.IsNullOrWhiteSpace(ciCommitShortSha) &&
                ciCommitShortSha.Length > 8)
            {
                var currentTime = DateTimeOffset.UtcNow;
                var unixTimestamp = currentTime.ToUnixTimeSeconds();
                var versionDateTime = currentTime.ToString("yyyy.MM.dd");
                if (!string.IsNullOrWhiteSpace(overrideDateVersion))
                {
                    versionDateTime = overrideDateVersion;
                }

                EngineBuildVersionJson? engineInfo;
                await using (var engineWorkspace = await _engineWorkspaceProvider.GetEngineWorkspace(
                    engineSpec,
                    string.Empty,
                    useStorageVirtualisation,
                    cancellationToken))
                {
                    engineInfo = GetEngineVersionInfo(engineWorkspace.Path);
                    if (engineInfo == null)
                    {
                        throw new BuildMisconfigurationException("Specified engine does not have a build.version file, but having version information is required in order to build and package plugins.");
                    }
                }

                var versionNumber = $"{unixTimestamp}{engineInfo.MinorVersion}";
                var versionName = $"{versionDateTime}-{engineInfo.MajorVersion}.{engineInfo.MinorVersion}-{ciCommitShortSha.Substring(0, 8)}";

                _logger.LogInformation($"Building as versioned package: {versionName}");
                return (versionName, versionNumber);
            }
            else
            {
                _logger.LogInformation($"Building as unversioned package");
                return ("Unversioned", "10000");
            }
        }
    }
}
