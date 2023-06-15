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

            var volumes = new SystemVolumes().ToList();
            Assert.NotEmpty(volumes);
            Assert.NotEmpty(volumes[0].VolumeName);
            Assert.NotEmpty(volumes[0].DeviceName);
            Assert.NotEmpty(volumes[0].VolumePathNames);
        }
    }
}