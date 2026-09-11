namespace Redpoint.Uet.Uat.Tests
{
    public class UatRuntimePatchingTests
    {
        [Fact]
        public async Task TestBinariesArePresent()
        {
            var manifestNames = typeof(UetUatServiceExtensions).Assembly.GetManifestResourceNames();
            Assert.Contains("Redpoint.Uet.Uat.Embedded.Redpoint.Uet.Patching.Runtime.dll", manifestNames);
            Assert.Contains("Redpoint.Uet.Uat.Embedded.0Harmony.dll", manifestNames);
        }
    }
}