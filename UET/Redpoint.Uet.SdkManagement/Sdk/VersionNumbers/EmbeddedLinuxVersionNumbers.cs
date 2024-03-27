namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class EmbeddedLinuxVersionNumbers : ILinuxVersionNumbers
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
                "Linux",
                "LinuxPlatformSDK.Versions.cs"));
        }

        internal static Task<string> ParseClangToolchainVersion(string linuxPlatformSdk)
        {
            var regex = new Regex("return \"([a-z0-9-_\\.]+)\"");
            foreach (Match match in regex.Matches(linuxPlatformSdk))
            {
                return Task.FromResult(match.Groups[1].Value);
            }
            throw new InvalidOperationException("Unable to find Clang version in LinuxPlatformSDK.Versions.cs");
        }

        public async Task<string> GetClangToolchainVersion(string unrealEnginePath)
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
    }
}
