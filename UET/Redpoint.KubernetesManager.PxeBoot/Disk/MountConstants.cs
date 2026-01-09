namespace Redpoint.KubernetesManager.PxeBoot.Disk
{
    internal class MountConstants
    {
        public const string LinuxBootMountPath = "/var/mount/boot";

        public const string LinuxProvisionMountPath = "/var/mount/provision";

        public const string LinuxOperatingSystemMountPath = "/var/mount/os";

        public const string LinuxRamdiskMountPath = "/";

        public const string WindowsBootDrive = "E:";

        public const string WindowsProvisionDrive = "D:";

        public const string WindowsOperatingSystemDrive = "C:";

        public const string WindowsRamdiskDrive = "X:";
    }
}
