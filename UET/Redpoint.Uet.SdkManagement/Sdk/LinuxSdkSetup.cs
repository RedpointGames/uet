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

    [SupportedOSPlatform("windows")]
    public class LinuxSdkSetup : ISdkSetup
    {
        private readonly ILogger<LinuxSdkSetup> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;

        public LinuxSdkSetup(
            ILogger<LinuxSdkSetup> logger,
            IProcessExecutor processExecutor,
            ISimpleDownloadProgress simpleDownloadProgress)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _simpleDownloadProgress = simpleDownloadProgress;
        }

        public string[] PlatformNames => new[] { "Linux" };

        public string CommonPlatformNameForPackageId => "Linux";

        internal static Task<string> ParseClangToolchainVersion(string linuxPlatformSdk)
        {
            var regex = new Regex("return \"([a-z0-9-_\\.]+)\"");
            foreach (Match match in regex.Matches(linuxPlatformSdk))
            {
                return Task.FromResult(match.Groups[1].Value);
            }
            throw new InvalidOperationException("Unable to find Clang version in LinuxPlatformSDK.Versions.cs");
        }

        private async Task<string> GetClangToolchainVersion(string unrealEnginePath)
        {
            var linuxPlatformSdk = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "Platform",
                "Linux",
                "LinuxPlatformSDK.Versions.cs")).ConfigureAwait(false);
            return await ParseClangToolchainVersion(linuxPlatformSdk).ConfigureAwait(false);
        }

        public Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            return GetClangToolchainVersion(unrealEnginePath);
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            var clangToolchain = await GetClangToolchainVersion(unrealEnginePath).ConfigureAwait(false);

            _logger.LogInformation($"Downloading Linux cross-compile toolchain {clangToolchain}...");
            using (var client = new HttpClient())
            {
                using (var target = new FileStream(Path.Combine(sdkPackagePath, "toolchainextract.exe"), FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var downloadUrl = $"https://cdn.unrealengine.com/CrossToolchain_Linux/{clangToolchain.Trim()}.exe";
                    await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                        client,
                        downloadUrl,
                        async stream => await stream.CopyToAsync(target, cancellationToken).ConfigureAwait(false),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation($"Extracting Linux cross-compile toolchain {clangToolchain}...");
            using (var zstream = new FileStream(Path.Combine(sdkPackagePath, "7z.exe"), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var sstream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.SdkManagement.7z.exe"))
                {
                    await sstream!.CopyToAsync(zstream, cancellationToken).ConfigureAwait(false);
                }
            }
            Directory.CreateDirectory(Path.Combine(sdkPackagePath, "SDK"));
            var exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = Path.Combine(sdkPackagePath, "7z.exe"),
                    Arguments = new[] { "x", "..\\toolchainextract.exe" },
                    WorkingDirectory = Path.Combine(sdkPackagePath, "SDK")
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new SdkSetupPackageGenerationFailedException("Failed to extract NSIS installer with 7-Zip.");
            }

            File.Delete(Path.Combine(sdkPackagePath, "7z.exe"));
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
