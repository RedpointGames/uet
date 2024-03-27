namespace Redpoint.Uet.SdkManagement.Tests
{
    using Redpoint.Uet.SdkManagement;
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using System.Runtime.Versioning;

    public class MacEnvironmentSetupTests
    {
        [Fact]
        [SupportedOSPlatform("macos")]
        public async Task CanParseVersions()
        {
            var xcodeVersion = await EmbeddedMacVersionNumbers.ParseXcodeVersion(@"
namespace TestNamespace
{
	internal class TestClass
	{
		public string GetMainVersion()
		{
			return ""14.1"";
		}
    }
}
");
            Assert.Equal("14.1", xcodeVersion);
        }
    }
}