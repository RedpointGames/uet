namespace Redpoint.Uet.SdkManagement
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.PackageManagement;
    using Redpoint.PathResolution;

    [SupportedOSPlatform("windows")]
    public class LinuxSdkSetup : ISdkSetup
    {
        private readonly ILogger<LinuxSdkSetup> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;
        private readonly IVersionNumberResolver _versionNumberResolver;
        private readonly IPackageManager _packageManager;
        private readonly IPathResolver _pathResolver;

        public LinuxSdkSetup(
            ILogger<LinuxSdkSetup> logger,
            IProcessExecutor processExecutor,
            ISimpleDownloadProgress simpleDownloadProgress,
            IVersionNumberResolver versionNumberResolver,
            IPackageManager packageManager,
            IPathResolver pathResolver)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _simpleDownloadProgress = simpleDownloadProgress;
            _versionNumberResolver = versionNumberResolver;
            _packageManager = packageManager;
            _pathResolver = pathResolver;
        }

        public IReadOnlyList<string> PlatformNames => new[] { "Linux" };

        public string CommonPlatformNameForPackageId => "Linux";

        public Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            return _versionNumberResolver.For<ILinuxVersionNumbers>(unrealEnginePath).GetClangToolchainVersion(unrealEnginePath);
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            var clangToolchain = await _versionNumberResolver.For<ILinuxVersionNumbers>(unrealEnginePath).GetClangToolchainVersion(unrealEnginePath).ConfigureAwait(false);

            _logger.LogInformation("Ensuring 7-zip is installed for fast extraction...");
            await _packageManager.InstallOrUpgradePackageToLatestAsync("7zip.7zip", cancellationToken);

            _logger.LogInformation("Locating 7-zip...");
            var _7z = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "7-Zip",
                "7z.exe");
            if (!File.Exists(_7z))
            {
                _logger.LogWarning("7-zip is not installed under Program Files (where we expect it to be). Attempting to find it on the PATH environment variable, but this may fail...");
                _7z = await _pathResolver.ResolveBinaryPath("7z");
            }

            _logger.LogInformation($"Downloading Linux cross-compile toolchain {clangToolchain}...");
            using (var client = new HttpClient())
            {
                using var target = new FileStream(Path.Combine(sdkPackagePath, "toolchainextract.exe"), FileMode.Create, FileAccess.Write, FileShare.None);

                var downloadUrl = $"https://cdn.unrealengine.com/CrossToolchain_Linux/{clangToolchain.Trim()}.exe";
                await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                    client,
                    new Uri(downloadUrl),
                    async stream => await stream.CopyToAsync(target, cancellationToken).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation($"Extracting Linux cross-compile toolchain {clangToolchain}...");
            Directory.CreateDirectory(Path.Combine(sdkPackagePath, "SDK"));
            var exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = _7z,
                    Arguments = new LogicalProcessArgument[] { "x", "..\\toolchainextract.exe" },
                    WorkingDirectory = Path.Combine(sdkPackagePath, "SDK")
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new SdkSetupPackageGenerationFailedException("Failed to extract NSIS installer with 7-Zip.");
            }

            File.Delete(Path.Combine(sdkPackagePath, "toolchainextract.exe"));
        }

        public Task<AutoSdkMapping[]> GetAutoSdkMappingsForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Array.Empty<AutoSdkMapping>());
        }

        public Task<EnvironmentForSdkUsage> GetRuntimeEnvironmentForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(new EnvironmentForSdkUsage
            {
                EnvironmentVariables = new Dictionary<string, string>()
                {
                    { "LINUX_MULTIARCH_ROOT", Path.Combine(sdkPackagePath, "SDK") },
                }
            });
        }

    }
}
