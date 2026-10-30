namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System.Runtime.Versioning;

    public readonly record struct MacPortableToolchainInfo
    {
        public required readonly string PortableToolchainVersion { get; init; }
        public required readonly string ClangSubdir { get; init; }
        public required readonly string OSSHeadersSubdir { get; init; }
    }

    internal interface IMacVersionNumbers : IVersionNumbers
    {
        [SupportedOSPlatform("macos")]
        Task<string> GetXcodeVersion(string unrealEnginePath);

        [SupportedOSPlatform("macos")]
        Task<MacPortableToolchainInfo?> GetPortableToolchainVersion(string unrealEnginePath, string xcodeVersion);

        [SupportedOSPlatform("windows")]
        Task<string> GetITunesVersion(string unrealEnginePath);
    }
}
