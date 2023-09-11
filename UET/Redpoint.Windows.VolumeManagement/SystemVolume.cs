namespace Redpoint.Windows.VolumeManagement
{
    /// <summary>
    /// Represents a Windows volume.
    /// </summary>
    public class SystemVolume
    {
        internal SystemVolume(
            string volumeName,
            string deviceName,
            string[] volumePathNames)
        {
            VolumeName = volumeName;
            DeviceName = deviceName;
            VolumePathNames = volumePathNames;
        }

        /// <summary>
        /// The volume name, in the format <code>\\?\Volume{Guid}\</code>.
        /// </summary>
        public string VolumeName { get; }

        /// <summary>
        /// The device name, in the format <code>\Device\HarddiskVolumeX</code>.
        /// </summary>
        public string DeviceName { get; }

        /// <summary>
        /// An array of path names that the volume is mounted to. These can either be drive letters in the format <code>C:\</code> or paths in the format <code>C:\SomeFolder\Mount\</code>.
        /// </summary>
        public IReadOnlyCollection<string> VolumePathNames { get; }
    }
}