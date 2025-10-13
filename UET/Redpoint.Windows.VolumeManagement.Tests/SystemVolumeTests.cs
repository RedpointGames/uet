namespace Redpoint.Windows.VolumeManagement.Tests
{
    using System.Runtime.Versioning;

    public class SystemVolumeTests
    {
        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public void CanQuerySystemVolumes()
        {
            Assert.SkipUnless(OperatingSystem.IsWindowsVersionAtLeast(6, 2), "Windows version too old.");
            // @note: This test does not work on GitHub Actions, presumably due to some weird sandboxing or volume mapping.
            Assert.SkipUnless(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTION")), "Skipped on GitHub actions.");

            var volumes = new SystemVolumes().ToList();
            Assert.NotEmpty(volumes);
            Assert.NotEmpty(volumes[0].VolumeName);
            Assert.NotEmpty(volumes[0].DeviceName);
            Assert.NotEmpty(volumes[0].VolumePathNames);
        }
    }
}