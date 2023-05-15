namespace Redpoint.SdkManagement.Tests
{
    using Redpoint.SdkManagement.WindowsSdk;
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
            VersionNumberRange.Parse(""14.34.31933"", ""14.34.99999""),
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
        private const string _versionCodeFragment = @"
namespace TestNamespace
{
    class VersionNumber
    {
        public string Value { get; set; }

        public static VersionNumber Parse(string value)
        {
            return new VersionNumber { Value = value };
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
";
        private const string _versionRangeSdkCodeFragment = @"
namespace TestNamespace
{
    class VersionNumberRange
    {
        public VersionNumber Min { get; set; }

        public static VersionNumberRange Parse(string min, string max)
        {
            return new VersionNumberRange { Min = VersionNumber.Parse(min) };
        }
    }
}
";

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanParseVersionsFromUnrealCSharp()
        {
            var versions = await WindowsSdkSetup.ParseVersions(
                _microsoftPlatformSdkCodeFragment,
                _versionCodeFragment,
                _versionRangeSdkCodeFragment);

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