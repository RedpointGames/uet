namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class EmbeddedAndroidVersionNumbers : IAndroidVersionNumbers
    {
        public int Priority => 100;

        public bool CanUse(string unrealEnginePath)
        {
            return File.Exists(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "Platform",
                "Android",
                "AndroidPlatformSDK.Versions.cs"));
        }

        internal static Task<string> ParseVersion(string androidPlatformSdk, string versionCategory)
        {
            var regex = new Regex("case \"([a-z-]+)\": return \"([a-z0-9-\\.]+)\"");
            foreach (Match match in regex.Matches(androidPlatformSdk))
            {
                if (match.Groups[1].Value == versionCategory)
                {
                    return Task.FromResult(match.Groups[2].Value);
                }
            }
            throw new InvalidOperationException($"Unable to find Android version for {versionCategory} in AndroidPlatformSDK.Versions.cs");
        }

        public async Task<(string platforms, string buildTools, string cmake, string ndk)> GetVersions(string unrealEnginePath)
        {
            var androidPlatformSdk = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "Platform",
                "Android",
                "AndroidPlatformSDK.Versions.cs")).ConfigureAwait(false);
            return (
                await ParseVersion(androidPlatformSdk, "platforms").ConfigureAwait(false),
                await ParseVersion(androidPlatformSdk, "build-tools").ConfigureAwait(false),
                await ParseVersion(androidPlatformSdk, "cmake").ConfigureAwait(false),
                await ParseVersion(androidPlatformSdk, "ndk").ConfigureAwait(false));
        }
    }
}
