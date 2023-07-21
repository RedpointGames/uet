namespace Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk
{
    internal class WindowsSdkInstallerTarget
    {
        public required VersionNumber WindowsSdkPreferredVersion { get; set; }
        public required VersionNumber VisualCppMinimumVersion { get; set; }
        public required string[] SuggestedComponents { get; set; }
    }
}
