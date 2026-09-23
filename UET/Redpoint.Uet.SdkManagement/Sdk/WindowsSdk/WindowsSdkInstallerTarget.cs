namespace Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk
{
    internal class WindowsSdkInstallerTarget
    {
        public required VersionNumber WindowsSdkPreferredVersion { get; set; }
        public required VersionNumber MinimumVisualCppVersion { get; set; }
        public required List<VersionRange> PreferredVisualCppVersions { get; set; }
        public required List<VersionRange> BannedVisualCppVersions { get; set; }
        public required string[] SuggestedComponents { get; set; }
        public required Dictionary<string, string> MinimumRequiredClangVersions { get; set; }
        public required List<VersionRange> BannedClangVersions { get; set; }
        public required VersionNumber? MinimumClangVersion { get; set; }
    }
}
