namespace Redpoint.KubernetesManager.PxeBoot.Disk
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.InitializeOsPartition;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;

    internal class DefaultOperatingSystemPartitionManager : IOperatingSystemPartitionManager
    {
        private readonly IParted _parted;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<DefaultOperatingSystemPartitionManager> _logger;

        public DefaultOperatingSystemPartitionManager(
            IParted parted,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor,
            ILogger<DefaultOperatingSystemPartitionManager> logger)
        {
            _parted = parted;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            _logger = logger;
        }

        public async Task InitializeOperatingSystemDiskAsync(string diskPath, InitializeOsPartitionProvisioningStepFilesystem filesystem, CancellationToken cancellationToken)
        {
            var mountPath = MountConstants.LinuxOperatingSystemMountPath;
            var partitionPath = $"{diskPath}-part{PartitionConstants.OperatingSystemPartitionIndex}";

            var mount = await _pathResolver.ResolveBinaryPath("mount");
            var mtab = File.ReadAllText("/etc/mtab");

            if (mtab.Contains(mountPath, StringComparison.Ordinal))
            {
                _logger.LogInformation($"Unmounting {mountPath}...");

                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = await _pathResolver.ResolveBinaryPath("umount"),
                        Arguments = ["-f", mountPath],
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new UnableToProvisionSystemException($"Unable to unmount {mountPath} so it can be reinitialized.");
                }
            }
            else
            {
                _logger.LogInformation($"{mountPath} is not currently mounted.");
            }

            if (filesystem == InitializeOsPartitionProvisioningStepFilesystem.Ntfs)
            {
                _logger.LogInformation("Re-initializing operating system partition as NTFS...");

                await _parted.RunCommandAsync(diskPath, ["type", PartitionConstants.OperatingSystemPartitionIndex.ToString(CultureInfo.InvariantCulture), "ebd0a0a2-b9e5-4433-87c0-68b6b72699c7"], cancellationToken);

                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = await _pathResolver.ResolveBinaryPath("mkfs.ntfs"),
                        Arguments = ["-Q", "-F", "-L", PartitionConstants.OperatingSystemPartitionLabel, partitionPath],
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new UnableToProvisionSystemException($"mkfs.ntfs failed");
                }
            }
            else if (filesystem == InitializeOsPartitionProvisioningStepFilesystem.Ext4)
            {
                _logger.LogInformation("Re-initializing operating system partition as Ext4...");

                await _parted.RunCommandAsync(diskPath, ["type", PartitionConstants.OperatingSystemPartitionIndex.ToString(CultureInfo.InvariantCulture), "4f68bce3-e8cd-4db1-96e7-fbcaf984b709"], cancellationToken);

                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = await _pathResolver.ResolveBinaryPath("mkfs.ext4"),
                        Arguments = ["-F", "-L", PartitionConstants.OperatingSystemPartitionLabel, partitionPath],
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new UnableToProvisionSystemException($"mkfs.ext4 failed");
                }
            }
            else
            {
                throw new UnableToProvisionSystemException($"Unknown filesystem type specified for initializeOsPartition: {filesystem}");
            }
        }

        public async Task TryMountOperatingSystemDiskAsync(string diskPath, CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException();
            }

            var mountPath = MountConstants.LinuxOperatingSystemMountPath;
            var partitionPath = $"{diskPath}-part{PartitionConstants.OperatingSystemPartitionIndex}";

            Directory.CreateDirectory(mountPath);

            var mount = await _pathResolver.ResolveBinaryPath("mount");
            var mtab = File.ReadAllText("/etc/mtab");

            if (mtab.Contains(mountPath, StringComparison.Ordinal))
            {
                return;
            }

            var disk = await _parted.GetDiskAsync(diskPath, cancellationToken);
            if (disk.Partitions == null ||
                disk.Partitions.Length < PartitionConstants.OperatingSystemPartitionIndex ||
                disk.Partitions[PartitionConstants.OperatingSystemPartitionIndex - 1] == null)
            {
                return;
            }

            var filesystem = disk.Partitions[PartitionConstants.OperatingSystemPartitionIndex - 1]!.Filesystem;
            switch (filesystem)
            {
                case "ntfs":
                    {
                        var exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = await _pathResolver.ResolveBinaryPath("ntfsfix"),
                                Arguments = [partitionPath, "-d"],
                            },
                            CaptureSpecification.Passthrough,
                            cancellationToken);
                        if (exitCode != 0)
                        {
                            throw new UnableToProvisionSystemException($"fsck partition {PartitionConstants.OperatingSystemPartitionIndex} failed!");
                        }
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = mount,
                                Arguments = [partitionPath, "-t", "ntfs3", mountPath],
                            },
                            CaptureSpecification.Passthrough,
                            cancellationToken);
                        if (exitCode != 0)
                        {
                            throw new UnableToProvisionSystemException($"mount partition {PartitionConstants.OperatingSystemPartitionIndex} to {mountPath} failed!");
                        }
                        break;
                    }
                case "ext2":
                case "ext3":
                case "ext4":
                    {
                        var exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = await _pathResolver.ResolveBinaryPath("fsck"),
                                Arguments = ["-t", filesystem, partitionPath],
                            },
                            CaptureSpecification.Passthrough,
                            cancellationToken);
                        if (exitCode != 0)
                        {
                            throw new UnableToProvisionSystemException($"fsck partition {PartitionConstants.OperatingSystemPartitionIndex} failed!");
                        }
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = mount,
                                Arguments = [partitionPath, "-t", filesystem, mountPath],
                            },
                            CaptureSpecification.Passthrough,
                            cancellationToken);
                        if (exitCode != 0)
                        {
                            throw new UnableToProvisionSystemException($"mount partition {PartitionConstants.OperatingSystemPartitionIndex} to {mountPath} failed!");
                        }
                        break;
                    }
            }

        }
    }
}
