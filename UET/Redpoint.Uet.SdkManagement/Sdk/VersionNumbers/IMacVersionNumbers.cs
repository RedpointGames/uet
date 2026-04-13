namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System.Runtime.Versioning;

    internal interface IMacVersionNumbers : IVersionNumbers
    {
        [SupportedOSPlatform("macos")]
        Task<string> GetXcodeVersion(string unrealEnginePath);

        [SupportedOSPlatform("windows")]
        Task<string> GetITunesVersion(string unrealEnginePath);
    }
}
