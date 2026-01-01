namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.KubernetesManager.Configuration.Json;
    using Redpoint.KubernetesManager.Configuration.Sources;
    using Redpoint.KubernetesManager.Configuration.Types;
    using Redpoint.KubernetesManager.PxeBoot.Api;
    using Redpoint.KubernetesManager.PxeBoot.Disk;
    using Redpoint.KubernetesManager.PxeBoot.Provisioning.Step;
    using Redpoint.KubernetesManager.PxeBoot.ProvisioningStep;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Tpm;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http.Json;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class PxeBootProvisionClientCommandInstance : ICommandInstance
    {
        private readonly ILogger<PxeBootProvisionClientCommandInstance> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly IParted _parted;
        private readonly ITpmSecuredHttp _tpmSecuredHttp;
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
            PxeBootProvisionClientOptions options)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            _parted = parted;
            _tpmSecuredHttp = tpmSecuredHttp;
            _options = options;
            _provisioningSteps = provisioningSteps.ToList();

            _jsonSerializerContext = new KubernetesRkmJsonSerializerContext(new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new RkmNodeProvisionerStepJsonConverter(provisioningSteps),
                    new KubernetesDateTimeOffsetConverter(),
                }
            });
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

        private enum PlatformType
        {
            LinuxInitrd,
            Linux,
            Mac,
            Windows,
        }

        public async Task<int> ExecuteAsync(ICommandInvocationContext context)
        {
            var allowRecoveryShell = false;
            try
            {
                // Figure out the environment we're running in.
                PlatformType platformType;
                if (OperatingSystem.IsLinux())
                {
                    if (File.Exists("/rkm-initrd"))
                    {
                        _logger.LogInformation("Running on Linux initrd platform.");
                        platformType = PlatformType.LinuxInitrd;
                        allowRecoveryShell = true;
                    }
                    else
                    {
                        _logger.LogInformation("Running on Linux platform.");
                        platformType = PlatformType.Linux;
                    }
                }
                else if (OperatingSystem.IsMacOS())
                {
                    _logger.LogInformation("Running on macOS platform.");
                    platformType = PlatformType.Mac;
                }
                else if (OperatingSystem.IsWindows())
                {
                    _logger.LogInformation("Running on Windows platform.");
                    platformType = PlatformType.Windows;
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }

                // Determine the API address.
                string apiAddress;
                if (context.ParseResult.GetValueForOption(_options.Local))
                {
                    apiAddress = "127.0.0.1";
                }
                else
                {
                    if (platformType == PlatformType.LinuxInitrd || platformType == PlatformType.Linux)
                    {
                        var kernelCmdline = await File.ReadAllTextAsync("/proc/cmdline", context.GetCancellationToken());
                        var kernelCmdlineRegex = new Regex("rkm-api-address=(?<address>[0-9a-f:\\.]+)");
                        var kernelCmdlineRegexMatch = kernelCmdlineRegex.Match(kernelCmdline);
                        if (!kernelCmdlineRegexMatch.Success)
                        {
                            throw new UnableToProvisionSystemException("/proc/cmdline is missing the rkm-api-address= option.");
                        }
                        apiAddress = kernelCmdlineRegexMatch.Groups["address"].Value;
                    }
                    else if (platformType == PlatformType.Mac)
                    {
                        // @todo: Probably need to use UDP auto-discovery...
                        throw new PlatformNotSupportedException();
                    }
                    else
                    {
                        apiAddress = (await File.ReadAllTextAsync(
                            Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.System),
                                "rkm-api-address.txt"),
                            context.GetCancellationToken())).Trim();
                    }
                }
                _logger.LogInformation($"Using provisioner API address: {apiAddress}");

                // Create our TPM-secured HTTP client, and negotiate the client certificate.
                using var client = await _tpmSecuredHttp.CreateHttpClientAsync(
                    new Uri($"http://{apiAddress}:8790/api/node-provisioning/negotiate-certificate"),
                    context.GetCancellationToken());

                // Attempt to authorize ourselves with the cluster.
                var authorizeRequest = new AuthorizeNodeRequest
                {
                    CapablePlatforms = OperatingSystem.IsMacOS()
                        ? [RkmNodePlatform.Mac]
                        : [RkmNodePlatform.Windows, RkmNodePlatform.Linux],
                    Architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "amd64",
                };
                var secureEndpoint = $"https://{apiAddress}:8791/api/node-provisioning";
                AuthorizeNodeResponse authorizeResponse;
            retryNegotiate:
                var authorizeResponseRaw = await client.PutAsJsonAsync(
                    new Uri($"{secureEndpoint}/authorize"),
                    authorizeRequest,
                    ApiJsonSerializerContext.WithStringEnum.AuthorizeNodeRequest,
                    context.GetCancellationToken());
                if (authorizeResponseRaw.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("This node is not yet authorized to join the cluster. To authorize it, update the RkmNode object in the cluster. Waiting 1 minute and then checking again...");
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
                if (platformType == PlatformType.LinuxInitrd)
                {
                    // This function will throw if provisioning disks fails.
                    await ProvisionAndMountDisksAsync(context.GetCancellationToken());
                }

                // Now process provisioning steps.
                do
                {
                    var stepResponseRaw = await client.GetAsync(
                        new Uri($"{secureEndpoint}/step"),
                        context.GetCancellationToken());
                    if (stepResponseRaw.StatusCode == HttpStatusCode.NoContent)
                    {
                        _logger.LogInformation("No further provisioning steps to run.");
                        break;
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

                immediatelyStartNextStep:
                    await provisioningStep.ExecuteOnClientUncastedAsync(
                        currentStep?.DynamicSettings,
                        context.GetCancellationToken());

                    var stepCompleteResponseRaw = await client.GetAsync(
                        new Uri($"{secureEndpoint}/step-complete"),
                        context.GetCancellationToken());
                    if (stepCompleteResponseRaw.StatusCode == HttpStatusCode.NoContent)
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
    }
}
