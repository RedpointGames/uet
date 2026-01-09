namespace Redpoint.KubernetesManager.PxeBoot.Disk
{
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.InitializeOsPartition;
    using System.Threading.Tasks;

    internal interface IOperatingSystemPartitionManager
    {
        Task TryMountOperatingSystemDiskAsync(string diskPath, CancellationToken cancellationToken);

        Task InitializeOperatingSystemDiskAsync(string diskPath, InitializeOsPartitionProvisioningStepFilesystem filesystem, CancellationToken cancellationToken);
    }
}
