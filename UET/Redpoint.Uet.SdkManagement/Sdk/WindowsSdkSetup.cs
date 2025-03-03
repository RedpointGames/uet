namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    internal class WindowsSdkSetup : ISdkSetup
    {
        private readonly ILogger<WindowsSdkSetup> _logger;
        private readonly IVersionNumberResolver _versionNumberResolver;
        private readonly WindowsSdkInstaller _windowsSdkInstaller;

        public WindowsSdkSetup(
            ILogger<WindowsSdkSetup> logger,
            IVersionNumberResolver versionNumberResolver,
            WindowsSdkInstaller windowsSdkInstaller)
        {
            _logger = logger;
            _versionNumberResolver = versionNumberResolver;
            _windowsSdkInstaller = windowsSdkInstaller;
        }

        public IReadOnlyList<string> PlatformNames => new[] { "Windows", "Win64" };

        public string CommonPlatformNameForPackageId => "Windows";

        public async Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            var versions = await _versionNumberResolver.For<IWindowsVersionNumbers>(unrealEnginePath).GetWindowsVersionNumbersAsync(unrealEnginePath).ConfigureAwait(false);
            return $"{versions.WindowsSdkPreferredVersion}-{versions.VisualCppMinimumVersion}-v3";
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Retrieving desired versions from Unreal Engine source code...");
            var versions = await _versionNumberResolver.For<IWindowsVersionNumbers>(unrealEnginePath).GetWindowsVersionNumbersAsync(unrealEnginePath).ConfigureAwait(false);

            await _windowsSdkInstaller.InstallSdkToPath(versions, sdkPackagePath, cancellationToken).ConfigureAwait(false);
        }

        public Task<AutoSdkMapping[]> GetAutoSdkMappingsForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(new[]
            {
                new AutoSdkMapping
                {
                    RelativePathInsideAutoSdkPath = "Win64",
                    RelativePathInsideSdkPackagePath = ".",
                }
            });
        }

        public Task<EnvironmentForSdkUsage> GetRuntimeEnvironmentForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(new EnvironmentForSdkUsage
            {
                EnvironmentVariables = new Dictionary<string, string>(),
            });
        }
    }
}
