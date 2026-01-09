namespace Redpoint.KubernetesManager.PxeBoot.Server.Endpoints.UnauthenticatedFileTransfer
{
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <remarks>
    /// In WinPE environments where the provisioner is not running (usually very early on), we still want some
    /// core variables to be available, and to ensure that the disks have the correct drive letters assigned.
    /// </remarks>
    internal class WinPeUnauthenticatedFileTransferEndpoint : IUnauthenticatedFileTransferEndpoint
    {
        public string[] Prefixes => ["/winpe.bat"];

        public Task<Stream?> GetDownloadStreamAsync(
            UnauthenticatedFileTransferRequest request,
            CancellationToken cancellationToken)
        {
            var stream = new MemoryStream();
            using (var writer = new StreamWriter(stream, leaveOpen: true))
            {
                writer.Write(@$"set RKM_DISK_NUMBER=0" + "\r\n");
                writer.Write(@$"set RKM_DISK_PARTITION_BOOT_NUMBER={PartitionConstants.BootPartitionIndex}" + "\r\n");
                writer.Write(@$"set RKM_DISK_PARTITION_PROVISION_NUMBER={PartitionConstants.ProvisionPartitionIndex}" + "\r\n");
                writer.Write(@$"set RKM_DISK_PARTITION_OS_NUMBER={PartitionConstants.OperatingSystemPartitionIndex}" + "\r\n");
                writer.Write(@$"set RKM_MOUNT_BOOT={MountConstants.WindowsBootDrive}" + "\r\n");
                writer.Write(@$"set RKM_MOUNT_PROVISION={MountConstants.WindowsProvisionDrive}" + "\r\n");
                writer.Write(@$"set RKM_MOUNT_OS={MountConstants.WindowsOperatingSystemDrive}" + "\r\n");
                writer.Write(@$"set RKM_MOUNT_RAMDISK={MountConstants.WindowsRamdiskDrive}" + "\r\n");

                writer.Write(@$"echo select disk 0 >> diskpart_dismount.txt" + "\r\n");
                writer.Write(@$"echo select partition {PartitionConstants.BootPartitionIndex} >> diskpart_dismount.txt" + "\r\n");
                writer.Write(@$"echo remove all noerr >> diskpart_dismount.txt" + "\r\n");
                writer.Write(@$"echo select partition {PartitionConstants.ProvisionPartitionIndex} >> diskpart_dismount.txt" + "\r\n");
                writer.Write(@$"echo remove all noerr >> diskpart_dismount.txt" + "\r\n");
                writer.Write(@$"echo select partition {PartitionConstants.OperatingSystemPartitionIndex} >> diskpart_dismount.txt" + "\r\n");
                writer.Write(@$"echo remove all noerr >> diskpart_dismount.txt" + "\r\n");
                writer.Write(@$"diskpart /s diskpart_dismount.txt" + "\r\n");

                writer.Write(@$"echo select disk 0 >> diskpart_mount.txt" + "\r\n");
                writer.Write(@$"echo select partition {PartitionConstants.BootPartitionIndex} >> diskpart_mount.txt" + "\r\n");
                writer.Write(@$"echo assign letter={MountConstants.WindowsBootDrive[0]} noerr >> diskpart_mount.txt" + "\r\n");
                writer.Write(@$"echo select partition {PartitionConstants.ProvisionPartitionIndex} >> diskpart_mount.txt" + "\r\n");
                writer.Write(@$"echo assign letter={MountConstants.WindowsProvisionDrive[0]} noerr >> diskpart_mount.txt" + "\r\n");
                writer.Write(@$"echo select partition {PartitionConstants.OperatingSystemPartitionIndex} >> diskpart_mount.txt" + "\r\n");
                writer.Write(@$"echo assign letter={MountConstants.WindowsOperatingSystemDrive[0]} noerr >> diskpart_mount.txt" + "\r\n");
                writer.Write(@$"diskpart /s diskpart_mount.txt" + "\r\n");
            }
            stream.Seek(0, SeekOrigin.Begin);
            return Task.FromResult<Stream?>(stream);
        }
    }
}
