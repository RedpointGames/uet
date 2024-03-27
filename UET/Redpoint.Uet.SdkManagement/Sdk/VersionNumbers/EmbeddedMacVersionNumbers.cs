namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class EmbeddedMacVersionNumbers : IMacVersionNumbers
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
                "Mac",
                "ApplePlatformSDK.Versions.cs"));
        }

        internal static Task<string> ParseXcodeVersion(string applePlatformSdk)
        {
            var regex = new Regex("return \"([0-9\\.]+)\"");
            foreach (Match match in regex.Matches(applePlatformSdk))
            {
                // It's the first one because GetMainVersion() is
                // the first function in this file.
                return Task.FromResult(match.Groups[1].Value);
            }
            throw new InvalidOperationException("Unable to find Clang version in ApplePlatformSDK.Versions.cs");
        }

        public async Task<string> GetXcodeVersion(string unrealEnginePath)
        {
            var applePlatformSdk = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "Platform",
                "Mac",
                "ApplePlatformSDK.Versions.cs")).ConfigureAwait(false);
            return await ParseXcodeVersion(applePlatformSdk).ConfigureAwait(false);
        }
    }
}
