namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using System.Runtime.Versioning;
    using System.Text.Json;
    using System.Text.Json.Nodes;
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

        public bool SupportsTemporaryFolderSwapOnInstall => true;

        private const string _installLogicVersion = "v5";

        public async Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            var versions = await _versionNumberResolver.For<IWindowsVersionNumbers>(unrealEnginePath).GetWindowsVersionNumbersAsync(unrealEnginePath).ConfigureAwait(false);
            var selectedVcVersion = await _windowsSdkInstaller.GetPackageIdVersionSuffix(versions, cancellationToken).ConfigureAwait(false);

            return $"{versions.WindowsSdkPreferredVersion}-{selectedVcVersion}-{_installLogicVersion}";
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

        public async Task<EnvironmentForSdkUsage> GetRuntimeEnvironmentForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            var clangFormatPath = Path.Combine(
                sdkPackagePath,
                "LLVM",
                "x64",
                "bin",
                "clang-format.exe");
            if (File.Exists(clangFormatPath))
            {
                var vsLocalConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "VisualStudio");
                if (Directory.Exists(vsLocalConfigPath))
                {
                    foreach (var directory in Directory.GetDirectories(vsLocalConfigPath))
                    {
                        if (Path.GetFileName(directory).Contains('_', StringComparison.Ordinal) &&
                            !Path.GetFileName(directory).Contains("SettingsBackup", StringComparison.Ordinal))
                        {
                            JsonNode jsonDocument;
                            var settingsPath = Path.Combine(directory, "settings.json");
                            if (File.Exists(settingsPath))
                            {
                                jsonDocument = JsonNode.Parse(await File.ReadAllTextAsync(settingsPath, cancellationToken), documentOptions: new JsonDocumentOptions
                                {
                                    CommentHandling = JsonCommentHandling.Skip,
                                }) ?? JsonNode.Parse("{}")!;
                            }
                            else
                            {
                                jsonDocument = JsonNode.Parse("{}")!;
                            }

                            _logger.LogInformation($"Setting clang-format path for VS '{Path.GetFileName(directory)}'...");

                            jsonDocument.AsObject()["languages.cpp.codeStyle.formatting.general.useCustomClangFormatExe"] = true;
                            jsonDocument.AsObject()["languages.cpp.codeStyle.formatting.general.clangFormatExePath"] = clangFormatPath;

                            await File.WriteAllTextAsync(
                                settingsPath,
                                jsonDocument.ToJsonString(),
                                cancellationToken);
                        }
                    }
                }
            }

            return new EnvironmentForSdkUsage
            {
                EnvironmentVariables = new Dictionary<string, string>(),
            };
        }
    }
}
