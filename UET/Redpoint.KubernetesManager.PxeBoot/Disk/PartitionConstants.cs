namespace Redpoint.KubernetesManager.PxeBoot.Disk
{
    internal class PartitionConstants
    {
        public const string ProvisionPartitionLabel = "UetProvisioningDisk";

        public const string OperatingSystemPartitionLabel = "UetOsDisk";

        public const int BootPartitionIndex = 1;

        public const int MsrPartitionIndex = 2;

        public const int ProvisionPartitionIndex = 3;

        public const int OperatingSystemPartitionIndex = 4;
    }
}
