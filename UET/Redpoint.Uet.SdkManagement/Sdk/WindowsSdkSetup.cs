namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Versioning;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    internal class WindowsSdkSetup : ISdkSetup
    {
        private readonly ILogger<WindowsSdkSetup> _logger;
        private readonly WindowsSdkInstaller _windowsSdkInstaller;

        public WindowsSdkSetup(
            ILogger<WindowsSdkSetup> logger,
            WindowsSdkInstaller windowsSdkInstaller)
        {
            _logger = logger;
            _windowsSdkInstaller = windowsSdkInstaller;
        }

        public string[] PlatformNames => new[] { "Windows", "Win64" };

        public string CommonPlatformNameForPackageId => "Windows";

        private enum CurrentlyIn
        {
            None,
            PreferredWindowsSdk,
            PreferredVisualCpp,
            VsSuggestedComponents,
            Vs2022SuggestedComponents,
        }

        static readonly Regex _versionNumberRegex = new Regex("VersionNumber.Parse\\(\"([0-9\\.]+)\"\\)");
        static readonly Regex _versionNumberRangeRegex = new Regex("VersionNumberRange.Parse\\(\"([0-9\\.]+)\", \"([0-9\\.]+)\"\\)");
        static readonly Regex _elementLine = new Regex("\"([A-Za-z0-9\\.]+)\",");

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Assembly.Load is self-contained")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "GetType references a type created in memory")]
        internal static Task<(
            string windowsSdkPreferredVersion,
            string visualCppMinimumVersion,
            string[] suggestedComponents)> ParseVersions(
            string microsoftPlatformSdkFileContent)
        {
            string? windowsSdkPreferredVersion = null;
            string? visualCppMinimumVersion = null;
            List<string> suggestedComponents = new List<string>();

            var currentlyIn = CurrentlyIn.None;
            foreach (var line in microsoftPlatformSdkFileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith("//"))
                {
                    continue;
                }
                if (line.EndsWith(";"))
                {
                    currentlyIn = CurrentlyIn.None;
                }

                if (line.Contains(" PreferredWindowsSdkVersions "))
                {
                    currentlyIn = CurrentlyIn.PreferredWindowsSdk;
                    continue;
                }
                else if (line.Contains(" PreferredVisualCppVersions "))
                {
                    currentlyIn = CurrentlyIn.PreferredVisualCpp;
                    continue;
                }
                else if (line.Contains(" VisualStudioSuggestedComponents "))
                {
                    currentlyIn = CurrentlyIn.VsSuggestedComponents;
                    continue;
                }
                else if (line.Contains(" VisualStudio2022SuggestedComponents "))
                {
                    currentlyIn = CurrentlyIn.Vs2022SuggestedComponents;
                    continue;
                }

                switch (currentlyIn)
                {
                    case CurrentlyIn.PreferredWindowsSdk:
                        {
                            var match = _versionNumberRegex.Match(line);
                            if (match.Success)
                            {
                                windowsSdkPreferredVersion = match.Groups[1].Value;
                                currentlyIn = CurrentlyIn.None;
                            }
                            break;
                        }
                    case CurrentlyIn.PreferredVisualCpp:
                        {
                            var match = _versionNumberRangeRegex.Match(line);
                            if (match.Success && line.Contains("VS2022"))
                            {
                                visualCppMinimumVersion = match.Groups[1].Value;
                                currentlyIn = CurrentlyIn.None;
                            }
                            break;
                        }
                    case CurrentlyIn.VsSuggestedComponents:
                        {
                            var match = _elementLine.Match(line);
                            if (match.Success)
                            {
                                // @note: Turned off for now because they don't seem to be necessary
                                // to get Win64 platform building.
                                // suggestedComponents.Add(match.Groups[1].Value);
                            }
                            break;
                        }
                    case CurrentlyIn.Vs2022SuggestedComponents:
                        {
                            var match = _elementLine.Match(line);
                            if (match.Success)
                            {
                                // @note: Turned off for now because they don't seem to be necessary
                                // to get Win64 platform building.
                                // suggestedComponents.Add(match.Groups[1].Value);
                            }
                            break;
                        }
                }
            }

            if (windowsSdkPreferredVersion == null ||
                visualCppMinimumVersion == null)
            {
                throw new InvalidOperationException("Unable to parse versions from MicrosoftPlatformSDK.Versions.cs");
            }

            return Task.FromResult<(string windowsSdkPreferredVersion, string visualCppMinimumVersion, string[] suggestedComponents)>((
                windowsSdkPreferredVersion,
                visualCppMinimumVersion,
                suggestedComponents.ToArray()));
        }

        private async Task<WindowsSdkInstallerTarget> GetVersions(string unrealEnginePath)
        {
            var microsoftPlatformSdkFileContent = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "Platform",
                "Windows",
                "MicrosoftPlatformSDK.Versions.cs"));
            var rawVersions = await ParseVersions(microsoftPlatformSdkFileContent);
            return new WindowsSdkInstallerTarget
            {
                WindowsSdkPreferredVersion = VersionNumber.Parse(rawVersions.windowsSdkPreferredVersion),
                VisualCppMinimumVersion = VersionNumber.Parse(rawVersions.visualCppMinimumVersion),
                SuggestedComponents = rawVersions.suggestedComponents
            };
        }

        public async Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            var versions = await GetVersions(unrealEnginePath);
            return $"{versions.WindowsSdkPreferredVersion}-{versions.VisualCppMinimumVersion}";
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Retrieving desired versions from Unreal Engine source code...");
            var versions = await GetVersions(unrealEnginePath);

            await _windowsSdkInstaller.InstallSdkToPath(versions, sdkPackagePath, cancellationToken);
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
