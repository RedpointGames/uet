namespace Redpoint.Uet.SdkManagement.Tests
{
    using Redpoint.Uet.SdkManagement;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using System.Runtime.Versioning;

    public class WindowsEnvironmentSetupTests
    {
        private const string _microsoftPlatformSdkCodeFragment = @"
namespace TestNamespace
{
    class TestClass
    {
        static readonly VersionNumber[] PreferredWindowsSdkVersions = new VersionNumber[]
        {
            VersionNumber.Parse(""10.0.18362.0"")
        };
        static readonly VersionNumberRange[] PreferredVisualCppVersions = new VersionNumberRange[]
        {
            VersionNumberRange.Parse(""14.34.31933"", ""14.34.99999""), // VS2022
            VersionNumberRange.Parse(""14.29.30133"", ""14.29.99999""),
        };
        static readonly string[] VisualStudioSuggestedComponents = new string[]
        {
            ""Microsoft.VisualStudio.Workload.CoreEditor"",
            ""Microsoft.VisualStudio.Workload.NativeDesktop"",
            ""Microsoft.VisualStudio.Workload.NativeGame"",
            ""Microsoft.VisualStudio.Component.VC.Tools.x86.x64"",
            ""Microsoft.VisualStudio.Component.Windows10SDK"",
        };
        static readonly string[] VisualStudio2022SuggestedComponents = new string[]
        {
            ""Microsoft.VisualStudio.Workload.ManagedDesktop"",
            ""Microsoft.VisualStudio.Component.VC.14.34.17.4.x86.x64"",
            ""Microsoft.Net.Component.4.6.2.TargetingPack"",
        };
    }
}
";

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanParseVersionsFromUnrealCSharp()
        {
            var versions = await EmbeddedWindowsVersionNumbers.ParseVersions(
                _microsoftPlatformSdkCodeFragment);

            Assert.Equal("10.0.18362.0", versions.windowsSdkPreferredVersion);
            Assert.Equal("14.34.31933", versions.visualCppMinimumVersion);
        }

        [Fact]
        public void VersionNumberClassWorks()
        {
            var version = VersionNumber.Parse("10.0.18362.0");
            Assert.Equal(10, version.Major);
            Assert.Equal(0, version.Minor);
            Assert.Equal(18362, version.Patch);

            Assert.True(VersionNumber.Parse("10.0.18362.0") == VersionNumber.Parse("10.0.18362.0"));
            Assert.True(VersionNumber.Parse("10.0.18362.0") != VersionNumber.Parse("10.0.19362.0"));
            Assert.True(VersionNumber.Parse("10.0.18362.0") != VersionNumber.Parse("10.1.18362.0"));
            Assert.True(VersionNumber.Parse("10.0.18362.0") != VersionNumber.Parse("11.0.18362.0"));
            Assert.True(VersionNumber.Parse("10.0.18362.0") == VersionNumber.Parse("10.0.18362.1"));

            Assert.True(VersionNumber.Parse("14.34.31942") >= VersionNumber.Parse("14.34.31933"));
        }
    }
}