namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Hashing;
    using Redpoint.IO;
    using Redpoint.KubernetesManager.Configuration.Json;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using Redpoint.KubernetesManager.PxeBoot.Bootmgr;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using Redpoint.KubernetesManager.PxeBoot.FileTransfer;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.Reboot;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor;
    using Redpoint.Tpm;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PxeBootProvisionClientCommandInstance : ICommandInstance
    {
        private readonly ILogger<PxeBootProvisionClientCommandInstance> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly IParted _parted;
        private readonly ITpmSecuredHttp _tpmSecuredHttp;
        private readonly IEfiBootManager _efiBootManager;
        private readonly IFileTransferClient _fileTransferClient;
        private readonly IDurableOperation _durableOperation;
        private readonly IProvisionContextDiscoverer _provisionContextDiscoverer;
        private readonly IReboot _reboot;
        private readonly IOperatingSystemPartitionManager _operatingSystemPartitionManager;
        private readonly PxeBootProvisionClientOptions _options;
        private readonly List<IProvisioningStep> _provisioningSteps;
        private readonly KubernetesRkmJsonSerializerContext _jsonSerializerContext;

        public PxeBootProvisionClientCommandInstance(
            ILogger<PxeBootProvisionClientCommandInstance> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor,
            IParted parted,
            ITpmSecuredHttp tpmSecuredHttp,
            IEnumerable<IProvisioningStep> provisioningSteps,
            IEfiBootManager efiBootManager,
            IFileTransferClient fileTransferClient,
            IDurableOperation durableOperation,
            IProvisionContextDiscoverer provisionContextDiscoverer,
            IReboot reboot,
            IOperatingSystemPartitionManager operatingSystemPartitionManager,
            PxeBootProvisionClientOptions options)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            _parted = parted;
            _tpmSecuredHttp = tpmSecuredHttp;
            _efiBootManager = efiBootManager;
            _fileTransferClient = fileTransferClient;
            _durableOperation = durableOperation;
            _provisionContextDiscoverer = provisionContextDiscoverer;
            _reboot = reboot;
            _operatingSystemPartitionManager = operatingSystemPartitionManager;
            _options = options;
            _provisioningSteps = provisioningSteps.ToList();

            _jsonSerializerContext = KubernetesRkmJsonSerializerContext.CreateStringEnumWithAdditionalConverters(
                new RkmNodeProvisionerStepJsonConverter(provisioningSteps));
        }

        private async Task<string> ProvisionAndMountDisksAsync(
            string apiAddress,
            HttpClient client,
            ITpmSecuredHttpClientFactory clientFactory,
            CancellationToken cancellationToken)
        {
            var diskPaths = await _parted.GetDiskPathsAsync(cancellationToken);

            if (diskPaths.Length == 0)
            {
                throw new UnableToProvisionSystemException("There are zero disks present under /dev/disk/by-diskseq. This system can not be provisioned by PXE boot.");
            }
            else if (diskPaths.Length >= 2)
            {
                throw new UnableToProvisionSystemException("There is more than one disk present under /dev/disk/by-diskseq. Expected exactly one disk attached for provisioning via PXE boot to work.");
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

                var mkfsFat = await _pathResolver.ResolveBinaryPath("mkfs.fat");
                var mkfsNtfs = await _pathResolver.ResolveBinaryPath("mkfs.ntfs");

                await _parted.RunCommandAsync(diskPath, ["mkpart", "primary", "fat32", "1MiB", "2048MiB"], cancellationToken);
                await _parted.RunCommandAsync(diskPath, ["set", PartitionConstants.BootPartitionIndex.ToString(CultureInfo.InvariantCulture), "esp", "on"], cancellationToken);
                await _parted.RunCommandAsync(diskPath, ["mkpart", "primary", "2049MiB", "2081MiB"], cancellationToken);
                await _parted.RunCommandAsync(diskPath, ["type", PartitionConstants.MsrPartitionIndex.ToString(CultureInfo.InvariantCulture), "e3c9e316-0b5c-4db8-817d-f92df00215ae"], cancellationToken);
                await _parted.RunCommandAsync(diskPath, ["mkpart", "primary", "ntfs", "2082MiB", "34850MiB"], cancellationToken);
                await _parted.RunCommandAsync(diskPath, ["name", PartitionConstants.ProvisionPartitionIndex.ToString(CultureInfo.InvariantCulture), PartitionConstants.ProvisionPartitionLabel], cancellationToken);
                await _parted.RunCommandAsync(diskPath, ["type", PartitionConstants.ProvisionPartitionIndex.ToString(CultureInfo.InvariantCulture), "de94bba4-06d1-4d40-a16a-bfd50179d6ac"], cancellationToken);
                await _parted.RunCommandAsync(diskPath, ["mkpart", "primary", "34851MiB", "100%"], cancellationToken);
                await _parted.RunCommandAsync(diskPath, ["name", PartitionConstants.OperatingSystemPartitionIndex.ToString(CultureInfo.InvariantCulture), PartitionConstants.OperatingSystemPartitionLabel], cancellationToken);

                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = mkfsFat,
                        Arguments = [$"{diskPath}-part{PartitionConstants.BootPartitionIndex}"]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new UnableToProvisionSystemException($"mkfs.fat on partition {PartitionConstants.BootPartitionIndex} failed!");
                }
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = mkfsNtfs,
                        Arguments = ["-Q", "-L", "UetProvisioningDisk", $"{diskPath}-part{PartitionConstants.ProvisionPartitionIndex}"]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new UnableToProvisionSystemException($"mkfs.ntfs on partition {PartitionConstants.ProvisionPartitionIndex} failed!");
                }
            }
            else if (
                disk.Partitions == null ||
                disk.Partitions.Length != PartitionConstants.OperatingSystemPartitionIndex ||
                disk.Partitions[PartitionConstants.ProvisionPartitionIndex - 1] == null ||
                disk.Partitions[PartitionConstants.ProvisionPartitionIndex - 1]?.Name != PartitionConstants.ProvisionPartitionLabel ||
                disk.Partitions[PartitionConstants.OperatingSystemPartitionIndex - 1] == null ||
                disk.Partitions[PartitionConstants.OperatingSystemPartitionIndex - 1]?.Name != PartitionConstants.OperatingSystemPartitionLabel)
            {
                throw new UnableToProvisionSystemException("Disk is already initialized with partitions that are not recognised as a provisioned PXE boot setup. If you would like to provision this machine, you will need to manually remove the partitions on the disk (destroying all data) and then reboot.");
            }
            else
            {
                _logger.LogInformation("Machine disks are already provisioned.");
            }

            Directory.CreateDirectory(MountConstants.LinuxBootMountPath);
            Directory.CreateDirectory(MountConstants.LinuxProvisionMountPath);

            var mount = await _pathResolver.ResolveBinaryPath("mount");
            var mtab = File.ReadAllText("/etc/mtab");

            if (!mtab.Contains(MountConstants.LinuxBootMountPath, StringComparison.Ordinal))
            {
                for (int retryAttempt = 0; retryAttempt < 30; retryAttempt++)
                {
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath("fsck.vfat"),
                            Arguments = ["-a", $"{diskPath}-part{PartitionConstants.BootPartitionIndex}"],
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = mount,
                            Arguments = [$"{diskPath}-part{PartitionConstants.BootPartitionIndex}", MountConstants.LinuxBootMountPath],
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                    if (exitCode != 0)
                    {
                        if (retryAttempt == 29)
                        {
                            throw new UnableToProvisionSystemException($"mount partition {PartitionConstants.BootPartitionIndex} to {MountConstants.LinuxBootMountPath} failed!");
                        }
                        else
                        {
                            await Task.Delay(1000, cancellationToken);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            if (!mtab.Contains(MountConstants.LinuxProvisionMountPath, StringComparison.Ordinal))
            {
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = await _pathResolver.ResolveBinaryPath("ntfsfix"),
                        Arguments = [$"{diskPath}-part{PartitionConstants.ProvisionPartitionIndex}", "-d"],
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new UnableToProvisionSystemException($"fsck partition {PartitionConstants.ProvisionPartitionIndex} failed!");
                }
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = mount,
                        Arguments = [$"{diskPath}-part{PartitionConstants.ProvisionPartitionIndex}", "-t", "ntfs3", MountConstants.LinuxProvisionMountPath],
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new UnableToProvisionSystemException($"mount partition {PartitionConstants.ProvisionPartitionIndex} to {MountConstants.LinuxProvisionMountPath} failed!");
                }
            }
            await _operatingSystemPartitionManager.TryMountOperatingSystemDiskAsync(
                diskPath,
                cancellationToken);

            _logger.LogInformation("Reading EFI boot manager configuration...");
            var configuration = await _efiBootManager.GetBootManagerConfigurationAsync(
                cancellationToken);

            _logger.LogInformation($"EFI current boot: {configuration.BootCurrentId}");
            _logger.LogInformation($"EFI timeout: {configuration.Timeout} seconds");
            _logger.LogInformation($"EFI boot order: {string.Join(", ", configuration.BootOrder)}");
            foreach (var kv in configuration.BootEntries)
            {
                _logger.LogInformation($"EFI boot entry {kv.Key}, name '{kv.Value.Name}', {(kv.Value.Active ? "active" : "inactive")}, path '{kv.Value.Path}'");
            }

            if (Directory.Exists($"{MountConstants.LinuxBootMountPath}/EFI/RKM"))
            {
                await DirectoryAsync.DeleteAsync($"{MountConstants.LinuxBootMountPath}/EFI/RKM", true);
            }

            var entriesToRemove = configuration.BootEntries
                .Where(kv => !kv.Value.Path.Contains("/MAC(", StringComparison.Ordinal) &&
                             !kv.Value.Path.Contains(",DHCP,", StringComparison.Ordinal))
                .Select(k => k.Key)
                .ToList();
            if (entriesToRemove.Count > 0)
            {
                _logger.LogInformation("Removing existing EFI non-network entries...");
                foreach (var entry in entriesToRemove)
                {
                    await _efiBootManager.RemoveBootManagerEntryAsync(
                        entry,
                        CancellationToken.None);
                }
            }

            _logger.LogInformation("Reading EFI boot manager configuration...");
            configuration = await _efiBootManager.GetBootManagerConfigurationAsync(
                CancellationToken.None);

            async Task<List<int>> UpdateBootOrderAsync()
            {
                _logger.LogInformation("Updating EFI boot order...");
                var desiredBootOrder = configuration.BootEntries
                    .Values
                    .OrderByDescending(kv =>
                    {
                        if (!kv.Active)
                        {
                            // Inactive entries should be right before the UEFI fallback.
                            return -15;
                        }
                        else if (kv.Path.Contains("/MAC(", StringComparison.Ordinal) ||
                            kv.Path.Contains(",DHCP,", StringComparison.Ordinal))
                        {
                            // Always network boot first.
                            return 10;
                        }
                        else if (
                            kv.Name == "FrontPage" ||
                            kv.Path.Contains("MemoryMapped(", StringComparison.Ordinal))
                        {
                            // UEFI fallback entries that should be absolutely last because
                            // they halt the boot sequence and leave the machine idling.
                            // These are present on Hyper-V.
                            return -20;
                        }
                        else
                        {
                            // Other entries should be in the middle, since they will
                            // have been installed via provisioning.
                            return 0;
                        }
                    })
                    .ThenBy(x => configuration.BootOrder.IndexOf(x.BootId)) // Preserve boot order within priorities.
                    .Select(x => x.BootId)
                    .ToList();
                await _efiBootManager.SetBootManagerBootOrderAsync(
                    desiredBootOrder,
                    CancellationToken.None);
                return desiredBootOrder;
            }
            var desiredBootOrder = await UpdateBootOrderAsync();

            // Notify the API of our boot entries, and ask for the inactive list.
            _logger.LogInformation($"Synchronising our boot loader entries...");
            var bootEntriesJson = new List<RkmNodeStatusBootEntry>();
            foreach (var bootId in desiredBootOrder)
            {
                var bootEntry = configuration.BootEntries[bootId];
                bootEntriesJson.Add(new RkmNodeStatusBootEntry
                {
                    BootId = $"Boot{bootId:X4}",
                    Name = bootEntry.Name,
                    Path = bootEntry.Path,
                    Active = bootEntry.Active,
                });
            }
            var bootEntryResponse = await client.PutAsJsonAsync(
                new Uri($"https://{apiAddress}:8791/api/node-provisioning/sync-boot-entries"),
                bootEntriesJson,
                _jsonSerializerContext.ListRkmNodeStatusBootEntry,
                cancellationToken);
            bootEntryResponse.EnsureSuccessStatusCode();
            var inactiveBootEntries = await bootEntryResponse.Content.ReadFromJsonAsync(
                _jsonSerializerContext.IListString,
                cancellationToken) ?? [];
            _logger.LogInformation($"The following boot entries should be inactive: {string.Join(", ", inactiveBootEntries)}");
            var anyBootActiveChanges = false;
            foreach (var bootKv in configuration.BootEntries)
            {
                _logger.LogInformation($"Checking 'Boot{bootKv.Key:X4}'...");

                if (bootKv.Value.Active && inactiveBootEntries.Contains($"Boot{bootKv.Key:X4}", StringComparer.Ordinal))
                {
                    _logger.LogInformation($"Need to mark boot entry {bootKv.Key} as inactive...");
                    await _efiBootManager.SetBootManagerEntryActiveAsync(
                        bootKv.Key,
                        false,
                        cancellationToken);
                    anyBootActiveChanges = true;
                }
                else if (!bootKv.Value.Active && (!inactiveBootEntries.Contains($"Boot{bootKv.Key:X4}", StringComparer.Ordinal)))
                {
                    _logger.LogInformation($"Need to mark boot entry {bootKv.Key} as active...");
                    await _efiBootManager.SetBootManagerEntryActiveAsync(
                        bootKv.Key,
                        false,
                        cancellationToken);
                    anyBootActiveChanges = true;
                }
            }
            if (anyBootActiveChanges)
            {
                _logger.LogInformation($"Changes were made to the boot loader active states. Synchronising again...");

                // Update boot order to set the inactive entries after everything else.
                desiredBootOrder = await UpdateBootOrderAsync();

                // Notify API again with new active states.
                configuration = await _efiBootManager.GetBootManagerConfigurationAsync(
                    CancellationToken.None);
                bootEntriesJson = new List<RkmNodeStatusBootEntry>();
                foreach (var bootId in desiredBootOrder)
                {
                    var bootEntry = configuration.BootEntries[bootId];
                    bootEntriesJson.Add(new RkmNodeStatusBootEntry
                    {
                        BootId = $"Boot{bootId:X4}",
                        Name = bootEntry.Name,
                        Path = bootEntry.Path,
                        Active = bootEntry.Active,
                    });
                }
                bootEntryResponse = await client.PutAsJsonAsync(
                    new Uri($"https://{apiAddress}:8791/api/node-provisioning/sync-boot-entries"),
                    bootEntriesJson,
                    _jsonSerializerContext.ListRkmNodeStatusBootEntry,
                    cancellationToken);
                bootEntryResponse.EnsureSuccessStatusCode();
            }

            return diskPath;
        }

        private class DefaultProvisioningStepClientContext(
            bool isLocalTesting,
            HttpClient provisioningApiClient,
            HttpClient provisioningApiClientNoTimeout,
            string provisioningApiEndpointHttps,
            string provisioningApiEndpointHttp,
            string provisioningApiAddress,
            string authorizedNodeName,
            string aikFingerprint,
            Dictionary<string, string> parameterValues,
            string? diskPathLinux,
            ProvisioningClientPlatformType platform)
                : IProvisioningStepClientContext
        {
            public bool IsLocalTesting => isLocalTesting;

            public HttpClient ProvisioningApiClient => provisioningApiClient;

            public HttpClient ProvisioningApiClientNoTimeout => provisioningApiClientNoTimeout;

            public string ProvisioningApiEndpointHttps => provisioningApiEndpointHttps;

            public string ProvisioningApiEndpointHttp => provisioningApiEndpointHttp;

            public string ProvisioningApiAddress => provisioningApiAddress;

            public string AuthorizedNodeName => authorizedNodeName;

            public string AikFingerprint => aikFingerprint;

            public Dictionary<string, string> ParameterValues => parameterValues;

            public string? DiskPathLinux => diskPathLinux;

            public ProvisioningClientPlatformType Platform => platform;
        }

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            var allowRecoveryShell = false;
            try
            {
                // Get the provisioning context.
                var provisionContext = await _provisionContextDiscoverer.GetProvisionContextAsync(
                    context.ParseResult.GetValueForOption(_options.Local),
                    context.GetCancellationToken());
                allowRecoveryShell = provisionContext.AllowRecoveryShell;

                // Create our TPM-secured HTTP client, and negotiate the client certificate.
                var clientFactory = await _durableOperation.DurableOperationAsync(
                    async cancellationToken =>
                    {
                        return await _tpmSecuredHttp.CreateHttpClientFactoryAsync(
                            new Uri($"http://{provisionContext.ApiAddress}:8790/api/node-provisioning/negotiate-certificate"),
                            cancellationToken);
                    },
                    context.GetCancellationToken());
                using var client = clientFactory.Create();
                using var clientNoTimeout = clientFactory.Create();
                client.Timeout = TimeSpan.FromSeconds(5);
                clientNoTimeout.Timeout = TimeSpan.FromHours(1);

                // Attempt to authorize ourselves with the cluster.
                var authorizeRequest = new AuthorizeNodeRequest
                {
                    CapablePlatforms = OperatingSystem.IsMacOS()
                        ? [RkmNodePlatform.Mac]
                        : [RkmNodePlatform.Windows, RkmNodePlatform.Linux],
                    Architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "amd64",
                };
                var secureEndpoint = $"https://{provisionContext.ApiAddress}:8791/api/node-provisioning";
                AuthorizeNodeResponse authorizeResponse;
            retryNegotiate:
                var authorizeResponseRaw = await _durableOperation.DurableOperationAsync(
                    async cancellationToken =>
                    {
                        return await client.PutAsJsonAsync(
                            new Uri($"{secureEndpoint}/authorize"),
                            authorizeRequest,
                            ApiJsonSerializerContext.WithStringEnum.AuthorizeNodeRequest,
                            cancellationToken);
                    },
                    context.GetCancellationToken());
                if (authorizeResponseRaw.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var fingerprint = await authorizeResponseRaw.Content.ReadAsStringAsync();

                    _logger.LogError($"This node '{fingerprint}' is not yet authorized to join the cluster. To authorize it, update the RkmNode object in the cluster. Waiting 1 minute and then checking again...");
                    await Task.Delay(60000, context.GetCancellationToken());
                    goto retryNegotiate;
                }
                else if (authorizeResponseRaw.StatusCode == HttpStatusCode.OK)
                {
                    authorizeResponse = (await authorizeResponseRaw.Content.ReadFromJsonAsync(
                        ApiJsonSerializerContext.WithStringEnum.AuthorizeNodeResponse,
                        context.GetCancellationToken()))!;
                }
                else
                {
                    throw new UnableToProvisionSystemException($"Certificate negotiation endpoint returned unexpected response status code {authorizeResponseRaw.StatusCode}.");
                }
                _logger.LogInformation($"Authorized to join the cluster with node name '{authorizeResponse.NodeName}'.");

                // If we are running in the Linux initrd environment, make sure that we have provisioned the disks.
                string? linuxDiskPath = null;
                if (provisionContext.Platform == ProvisioningClientPlatformType.LinuxInitrd)
                {
                    // This function will throw if provisioning disks fails.
                    linuxDiskPath = await ProvisionAndMountDisksAsync(
                        provisionContext.ApiAddress,
                        client,
                        clientFactory,
                        context.GetCancellationToken());

                    // If we are in recovery, we need to tell the cluster to forcibly reprovision us and then reboot.
                    if (provisionContext.IsInRecovery)
                    {
                        _logger.LogInformation("Requesting reprovisioning from cluster...");
                        var forceReprovisionResponseRaw = await _durableOperation.DurableOperationAsync(
                            async cancellationToken =>
                            {
                                return await client.PutAsJsonAsync(
                                    new Uri($"{secureEndpoint}/force-reprovision"),
                                    new ForceReprovisionNodeRequest(),
                                    ApiJsonSerializerContext.WithStringEnum.ForceReprovisionNodeRequest,
                                    cancellationToken);
                            },
                            context.GetCancellationToken());
                        forceReprovisionResponseRaw.EnsureSuccessStatusCode();

                        _logger.LogInformation("Rebooting from recovery...");
                        await _reboot.RebootMachine(context.GetCancellationToken());
                        return 0;
                    }
                }

                // Now process provisioning steps.
                var clientContext = new DefaultProvisioningStepClientContext(
                    context.ParseResult.GetValueForOption(_options.Local),
                    client,
                    clientNoTimeout,
                    $"https://{provisionContext.ApiAddress}:8791",
                    $"http://{provisionContext.ApiAddress}:8790",
                    provisionContext.ApiAddress,
                    authorizeResponse.NodeName,
                    authorizeResponse.AikFingerprint,
                    authorizeResponse.ParameterValues,
                    linuxDiskPath,
                    provisionContext.Platform);
                var initial = true;
                do
                {
                    var @params = string.Empty;
                    if (initial)
                    {
                        @params = $"?initial=true&bootedFromStepIndex={provisionContext.BootedFromStepIndex}";
                    }

                    var stepResponseRaw = await _durableOperation.DurableOperationAsync(
                        async cancellationToken =>
                        {
                            return await client.GetAsync(
                                new Uri($"{secureEndpoint}/step{@params}"),
                                cancellationToken);
                        },
                        context.GetCancellationToken());
                    if (await HandleStepStatusCodeAsync(
                        stepResponseRaw,
                        client,
                        clientContext,
                        secureEndpoint,
                        context.GetCancellationToken()))
                    {
                        return 0;
                    }
                    stepResponseRaw.EnsureSuccessStatusCode();

                    var currentStep = await stepResponseRaw.Content.ReadFromJsonAsync(
                        _jsonSerializerContext.RkmNodeProvisionerStep,
                        context.GetCancellationToken());
                    var provisioningStep = _provisioningSteps.FirstOrDefault(x => string.Equals(x.Type, currentStep?.Type, StringComparison.OrdinalIgnoreCase));
                    if (provisioningStep == null)
                    {
                        throw new UnableToProvisionSystemException($"The provisioning step type '{currentStep?.Type}' does not exist on the client.");
                    }

                    initial = false;

                immediatelyStartNextStep:
                    _logger.LogInformation($"Now executing step '{currentStep?.Type}'...");
                    await provisioningStep.ExecuteOnClientUncastedAsync(
                        currentStep?.DynamicSettings,
                        clientContext,
                        context.GetCancellationToken());

                    if (provisioningStep.Flags.HasFlag(ProvisioningStepFlags.AssumeCompleteWhenIpxeScriptFetched))
                    {
                        _logger.LogInformation("Provisioning step completes on next iPXE script fetch. Exiting now.");
                        return 0;
                    }
                    else
                    {
                        var stepCompleteResponseRaw = await _durableOperation.DurableOperationAsync(
                            async cancellationToken =>
                            {
                                return await client.GetAsync(
                                    new Uri($"{secureEndpoint}/step-complete"),
                                    cancellationToken);
                            },
                            context.GetCancellationToken());
                        if (await HandleStepStatusCodeAsync(
                            stepCompleteResponseRaw,
                            client,
                            clientContext,
                            secureEndpoint,
                            context.GetCancellationToken()))
                        {
                            return 0;
                        }
                        else if (stepCompleteResponseRaw.StatusCode == HttpStatusCode.PartialContent)
                        {
                            // We didn't implicitly get the next step (there might be none). Loop
                            // again and exit if /step also returns 204 No Content.
                            continue;
                        }
                        stepCompleteResponseRaw.EnsureSuccessStatusCode();

                        currentStep = await stepCompleteResponseRaw.Content.ReadFromJsonAsync(
                            _jsonSerializerContext.RkmNodeProvisionerStep,
                            context.GetCancellationToken());
                        provisioningStep = _provisioningSteps.FirstOrDefault(x => string.Equals(x.Type, currentStep?.Type, StringComparison.OrdinalIgnoreCase));
                        if (provisioningStep == null)
                        {
                            throw new UnableToProvisionSystemException($"The provisioning step type '{currentStep?.Type}' does not exist on the client.");
                        }
                        goto immediatelyStartNextStep;
                    }
                }
                while (!context.GetCancellationToken().IsCancellationRequested);

                return 0;
            }
            catch (Exception ex)
            {
                // @todo: Report error to API address if we have that.

                if (ex is UnableToProvisionSystemException)
                {
                    _logger.LogError(ex.Message);
                }
                else
                {
                    _logger.LogError(ex, "Unexpected exception!");
                }

                if (allowRecoveryShell)
                {
                    _logger.LogInformation("Starting recovery shell...");
                    return await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = await _pathResolver.ResolveBinaryPath(OperatingSystem.IsWindows() ? "powershell" : "bash"),
                            Arguments = []
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                }
                else
                {
                    return 1;
                }
            }
        }

        private async Task<bool> HandleStepStatusCodeAsync(
            HttpResponseMessage stepResponse,
            HttpClient client,
            IProvisioningStepClientContext clientContext,
            string secureEndpoint,
            CancellationToken cancellationToken)
        {
            if (stepResponse.StatusCode == HttpStatusCode.Conflict)
            {
                _logger.LogError("Provisioning configuration changed during provisioning. Restarting to fresh environment.");

                _logger.LogInformation("Scheduling reboot on client...");
                var rebootProvisioningStep = _provisioningSteps.First(x => x.Type == "reboot");
                await rebootProvisioningStep.ExecuteOnClientUncastedAsync(
                    null,
                    clientContext,
                    cancellationToken);
                return true;
            }
            else if (stepResponse.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                _logger.LogError("Provisioning configuration on server is no longer valid. Restarting to go through authorization checks again.");

                _logger.LogInformation("Scheduling reboot on client...");
                var rebootProvisioningStep = _provisioningSteps.First(x => x.Type == "reboot");
                await rebootProvisioningStep.ExecuteOnClientUncastedAsync(
                    null,
                    clientContext,
                    cancellationToken);
                return true;
            }
            else if (stepResponse.StatusCode == HttpStatusCode.NoContent)
            {
                _logger.LogInformation("No further provisioning steps to run, requesting reboot to disk on server...");
                var rebootToDiskResponse = await client.GetAsync(new Uri($"{secureEndpoint}/reboot-to-disk"), cancellationToken);
                rebootToDiskResponse.EnsureSuccessStatusCode();

                _logger.LogInformation("Scheduling reboot on client...");
                var rebootProvisioningStep = _provisioningSteps.First(x => x.Type == "reboot");
                await rebootProvisioningStep.ExecuteOnClientUncastedAsync(
                    null,
                    clientContext,
                    cancellationToken);
                return true;
            }

            return false;
        }
    }
}
