namespace Redpoint.KubernetesManager.PxeBoot
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;

    internal class PxeBootInitrdBootstrapCommand : ICommandDescriptorProvider
    {
        public static CommandDescriptor Descriptor => CommandDescriptor.NewBuilder()
            .WithInstance<PxeBootInitrdBootstrapCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("initrd-bootstrap", "Initialize this machine for booting Linux or Windows RKM nodes from PXE Boot.");
                })
            .WithRuntimeServices(
                (_, services, _) =>
                {
                    services.AddSingleton<IParted, DefaultParted>();
                })
            .Build();

        private class PxeBootInitrdBootstrapCommandInstance : ICommandInstance
        {
            private readonly ILogger<PxeBootInitrdBootstrapCommandInstance> _logger;
            private readonly IPathResolver _pathResolver;
            private readonly IProcessExecutor _processExecutor;
            private readonly IParted _parted;

            public PxeBootInitrdBootstrapCommandInstance(
                ILogger<PxeBootInitrdBootstrapCommandInstance> logger,
                IPathResolver pathResolver,
                IProcessExecutor processExecutor,
                IParted parted)
            {
                _logger = logger;
                _pathResolver = pathResolver;
                _processExecutor = processExecutor;
                _parted = parted;
            }

            private async Task<int> ExecuteInInitrdAsync(CancellationToken cancellationToken)
            {
                await ProvisionAndMountDisksAsync(cancellationToken);

                throw new UnableToProvisionSystemException("Not yet implemented.");
            }

            private async Task ProvisionAndMountDisksAsync(CancellationToken cancellationToken)
            {
                var diskPaths = await _parted.GetDiskPathsAsync(cancellationToken);

                if (diskPaths.Length == 0)
                {
                    throw new UnableToProvisionSystemException("There are zero disks present under /dev/sd[a-z]. This system can not be provisioned by PXE boot.");
                }
                else if (diskPaths.Length >= 2)
                {
                    throw new UnableToProvisionSystemException("There is more than one disk present under /dev/sd[a-z]. Expected exactly one disk attached for provisioning via PXE boot to work.");
                }

                var diskPath = diskPaths[0];
                _logger.LogInformation($"Using disk {diskPath}");

                var disk = await _parted.GetDiskAsync(diskPath, cancellationToken);
                if (disk.Label == "unknown")
                {
                    _logger.LogInformation("Initializing disk as it is neither MBR nor GPT...");
                    await _parted.RunCommandAsync(diskPath, ["mktable", "gpt"], cancellationToken);
                    disk = await _parted.GetDiskAsync(diskPath, cancellationToken);
                }
                else if (disk.Label != "gpt")
                {
                    throw new UnableToProvisionSystemException("Disk is already initialized and it is not a GPT-based disk!");
                }

                int exitCode;
                if (disk.Partitions != null &&
                    disk.Partitions.Length == 0)
                {
                    _logger.LogInformation("The primary disk has no partitions and this machine can be provisioned for PXE boot.");
                    _logger.LogInformation("Please type 'PROVISION' and hit Enter to continue.");

                    var mkfsFat = await _pathResolver.ResolveBinaryPath("mkfs.fat");
                    var mkfsNtfs = await _pathResolver.ResolveBinaryPath("mkfs.ntfs");

                    var input = Console.ReadLine();
                    if (!string.Equals(input?.Trim(), "PROVISION", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new UnableToProvisionSystemException("You didn't type 'PROVISION', so this machine can't be provisioned.");
                    }

                    await _parted.RunCommandAsync(diskPath, ["mkpart", "primary", "fat32", "1MiB", "2048MiB"], cancellationToken);
                    await _parted.RunCommandAsync(diskPath, ["set", "1", "esp", "on"], cancellationToken);
                    await _parted.RunCommandAsync(diskPath, ["mkpart", "primary", "2049MiB", "2081MiB"], cancellationToken);
                    await _parted.RunCommandAsync(diskPath, ["type", "2", "e3c9e316-0b5c-4db8-817d-f92df00215ae"], cancellationToken);
                    await _parted.RunCommandAsync(diskPath, ["mkpart", "primary", "ntfs", "2082MiB", "100%"], cancellationToken);
                    await _parted.RunCommandAsync(diskPath, ["name", "3", "UetBootDisk"], cancellationToken);

                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = mkfsFat,
                            Arguments = [$"{diskPath}1"]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        throw new UnableToProvisionSystemException("mkfs.fat on partition 1 failed!");
                    }
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = mkfsNtfs,
                            Arguments = ["-Q", "-L", "UetBootDisk", $"{diskPath}3"]
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        throw new UnableToProvisionSystemException("mkfs.ntfs on partition 3 failed!");
                    }
                }
                else if (
                    disk.Partitions == null ||
                    disk.Partitions.Length != 3 ||
                    disk.Partitions[2] == null ||
                    disk.Partitions[2]?.Name != "UetBootDisk")
                {
                    throw new UnableToProvisionSystemException("Disk is already initialized with partitions that are not recognised as a provisioned PXE boot setup. If you would like to provision this machine, you will need to manually remove the partitions on the disk (destroying all data) and then reboot.");
                }
                else
                {
                    _logger.LogInformation("Machine is already provisioned for PXE boot.");
                }

                Directory.CreateDirectory("/var/mount/boot");
                Directory.CreateDirectory("/var/mount/images");

                var mount = await _pathResolver.ResolveBinaryPath("mount");

                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = mount,
                        Arguments = [$"{diskPath}1", "/var/mount/boot"],
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new UnableToProvisionSystemException("mount partition 1 to /var/mount/boot failed!");
                }
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = mount,
                        Arguments = [$"{diskPath}3", "-t", "ntfs3", "/var/mount/images"],
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new UnableToProvisionSystemException("mount partition 3 to /var/mount/images failed!");
                }
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                if (!OperatingSystem.IsLinux() ||
                    !File.Exists("/rkm-initrd"))
                {
                    _logger.LogError("This command must be run in the RKM initrd environment.");
                    return 1;
                }

                try
                {
                    return await ExecuteInInitrdAsync(context.GetCancellationToken());
                }
                catch (Exception ex)
                {
                    if (ex is UnableToProvisionSystemException)
                    {
                        _logger.LogError(ex.Message);
                    }
                    else
                    {
                        _logger.LogError(ex, "Unexpected exception!");
                    }

                    _logger.LogInformation("Starting recovery shell...");
                    return await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath("bash"),
                            Arguments = []
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                }
            }
        }
    }
}
