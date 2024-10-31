namespace Redpoint.Windows.VolumeManagement.Tests
{
    using System.Runtime.Versioning;

    public class SystemVolumeTests
    {
        [SkippableFact]
        [SupportedOSPlatform("windows6.2")]
        public void CanQuerySystemVolumes()
        {
            Skip.IfNot(OperatingSystem.IsWindowsVersionAtLeast(6, 2));
            // @note: This test does not work on GitHub Actions, presumably due to some weird sandboxing or volume mapping.
            Skip.IfNot(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTION")));

            var volumes = new SystemVolumes().ToList();
            Assert.NotEmpty(volumes);
            Assert.NotEmpty(volumes[0].VolumeName);
            Assert.NotEmpty(volumes[0].DeviceName);
            Assert.NotEmpty(volumes[0].VolumePathNames);
        }
    }
}