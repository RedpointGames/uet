namespace Redpoint.Windows.VolumeManagement.Tests
{
    using System.Runtime.Versioning;

    public class SystemVolumeTests
    {
        [Fact]
        [SupportedOSPlatform("windows6.2")]
        public void CanQuerySystemVolumes()
        {
            var volumes = new SystemVolumes().ToList();
            Assert.NotEmpty(volumes);
            Assert.NotEmpty(volumes[0].VolumeName);
            Assert.NotEmpty(volumes[0].DeviceName);
            Assert.NotEmpty(volumes[0].VolumePathNames);
        }
    }
}