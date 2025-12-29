namespace Redpoint.KubernetesManager.PxeBoot.Client
{
    using Microsoft.Extensions.Logging;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    internal class DefaultProvisionContextDiscoverer : IProvisionContextDiscoverer
    {
        private readonly ILogger<DefaultProvisionContextDiscoverer> _logger;

        public DefaultProvisionContextDiscoverer(
            ILogger<DefaultProvisionContextDiscoverer> logger)
        {
            _logger = logger;
        }

        public async Task<ProvisionContext> GetProvisionContextAsync(
            bool isLocal,
            CancellationToken cancellationToken)
        {
            bool allowRecoveryShell = false;

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
            int bootedFromStepIndex = -1;
            var isInRecovery = false;
            if (isLocal)
            {
                apiAddress = "127.0.0.1";
            }
            else
            {
                if (platformType == PlatformType.LinuxInitrd || platformType == PlatformType.Linux)
                {
                    var kernelCmdline = await File.ReadAllTextAsync("/proc/cmdline", cancellationToken);
                    var kernelCmdlineAddressRegex = new Regex("rkm-api-address=(?<address>[0-9a-f:\\.]+)");
                    var kernelCmdlineAddressRegexMatch = kernelCmdlineAddressRegex.Match(kernelCmdline);
                    var kernelCmdlineBootStepIndexRegex = new Regex("rkm-booted-from-step-index=(?<index>[0-9-]+)");
                    var kernelCmdlineBootStepIndexRegexMatch = kernelCmdlineBootStepIndexRegex.Match(kernelCmdline);
                    if (!kernelCmdlineAddressRegexMatch.Success)
                    {
                        throw new UnableToProvisionSystemException("/proc/cmdline is missing the rkm-api-address= option.");
                    }
                    apiAddress = kernelCmdlineAddressRegexMatch.Groups["address"].Value;
                    if (kernelCmdline.Contains("rkm-in-recovery", StringComparison.Ordinal))
                    {
                        _logger.LogInformation("RKM is running in recovery mode.");
                        isInRecovery = true;
                    }
                    else
                    {
                        if (!kernelCmdlineBootStepIndexRegexMatch.Success)
                        {
                            throw new UnableToProvisionSystemException("/proc/cmdline is missing the rkm-booted-from-step-index= option.");
                        }
                        bootedFromStepIndex = int.Parse(kernelCmdlineBootStepIndexRegexMatch.Groups["index"].Value, CultureInfo.InvariantCulture);
                    }
                }
                else if (platformType == PlatformType.Mac)
                {
                    // @todo: Probably need to use UDP auto-discovery...
                    throw new PlatformNotSupportedException();
                }
                else
                {
                    WindowsRkmProvisionContext jsonContext;
                    using (var stream = new FileStream(
                        Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.System),
                            "rkm-provision-context.json"),
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read))
                    {
                        jsonContext = (await JsonSerializer.DeserializeAsync(
                            stream,
                            WindowsRkmProvisionJsonSerializerContext.Default.WindowsRkmProvisionContext,
                            cancellationToken))!;
                    }

                    allowRecoveryShell = false;
                    apiAddress = jsonContext.ApiAddress;
                    isInRecovery = jsonContext.IsInRecovery;
                    bootedFromStepIndex = jsonContext.BootedFromStepIndex;
                }
            }
            _logger.LogInformation($"Using provisioner API address: {apiAddress}");

            return new ProvisionContext
            {
                AllowRecoveryShell = allowRecoveryShell,
                Platform = platformType,
                ApiAddress = apiAddress,
                IsInRecovery = isInRecovery,
                BootedFromStepIndex = bootedFromStepIndex,
            };
        }
    }
}
