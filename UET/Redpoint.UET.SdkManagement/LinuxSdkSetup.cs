namespace Redpoint.UET.SdkManagement
{
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.CSharp;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor;
    using System.IO;
    using System.Reflection;

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

        public string PlatformName => "Linux";

        internal static async Task<string> ParseClangToolchainVersion(string linuxPlatformSdk)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(linuxPlatformSdk);
            var syntaxRoot = await syntaxTree.GetRootAsync();
            var version = syntaxRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(x => x.Identifier.Text == "GetMainVersion")
                .First()
                .DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .First()
                .Expression!
                .GetFirstToken()
                .Value!
                .ToString();
            return version!;
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
                "LinuxPlatformSDK.Versions.cs"));
            return await ParseClangToolchainVersion(linuxPlatformSdk);
        }

        public Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            return GetClangToolchainVersion(unrealEnginePath);
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            var clangToolchain = await GetClangToolchainVersion(unrealEnginePath);

            _logger.LogInformation($"Downloading Linux cross-compile toolchain {clangToolchain}...");
            using (var client = new HttpClient())
            {
                using (var target = new FileStream(Path.Combine(sdkPackagePath, "toolchainextract.exe"), FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var downloadUrl = $"https://cdn.unrealengine.com/CrossToolchain_Linux/{clangToolchain.Trim()}.exe";
                    await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                        client,
                        downloadUrl,
                        async stream => await stream.CopyToAsync(target, cancellationToken),
                        cancellationToken);
                }
            }

            _logger.LogInformation($"Extracting Linux cross-compile toolchain {clangToolchain}...");
            using (var zstream = new FileStream(Path.Combine(sdkPackagePath, "7z.exe"), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var sstream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.UET.SdkManagement.7z.exe"))
                {
                    await sstream!.CopyToAsync(zstream, cancellationToken);
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
                cancellationToken);
            if (exitCode != 0)
            {
                throw new SdkSetupPackageGenerationFailedException("Failed to extract NSIS installer with 7-Zip.");
            }

            File.Delete(Path.Combine(sdkPackagePath, "7z.exe"));
            File.Delete(Path.Combine(sdkPackagePath, "toolchainextract.exe"));
        }

        public Task<EnvironmentForSdkUsage> EnsureSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
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
