namespace Redpoint.Uet.BuildPipeline.BuildGraph.MobileProvisioning
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Configuration.Engine;
    using System;
    using System.Collections.Generic;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;

    [SupportedOSPlatform("macos")]
    internal class MacMobileProvisioning : IMobileProvisioning
    {
        private readonly ILogger<MacMobileProvisioning> _logger;
        private readonly IProcessExecutor _processExecutor;

        public MacMobileProvisioning(
            ILogger<MacMobileProvisioning> logger,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _processExecutor = processExecutor;
        }

        public async Task InstallMobileProvisions(
            string enginePath,
            bool isEngineBuild,
            IEnumerable<BuildConfigMobileProvision> mobileProvisions,
            CancellationToken cancellationToken)
        {
            var provisioningProfilesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "MobileDevice",
                "Provisioning Profiles");
            Directory.CreateDirectory(provisioningProfilesRoot);

            foreach (var mobileProvision in mobileProvisions)
            {
                var keychainPassword = Environment.GetEnvironmentVariable(mobileProvision.KeychainPasswordEnvironmentVariable ?? "UET_MAC_USER_KEYCHAIN_PASSWORD");
                if (string.IsNullOrWhiteSpace(keychainPassword))
                {
                    _logger.LogError($"Unable to install mobile provisioning certificate '{mobileProvision.AppleProvidedCertificatePath}' because no Keychain password was provided. Store the Keychain password in an environment variable called or 'UET_MAC_USER_KEYCHAIN_PASSWORD' or an environment variable specified by the 'KeychainPasswordEnvironmentVariable' property in BuildConfig.json.");
                    continue;
                }

                // Unlock the Keychain.
                _logger.LogInformation("Unlocking Keychain if necessary...");
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/usr/bin/security",
                        Arguments = new LogicalProcessArgument[]
                        {
                            "unlock-keychain",
                            "-p",
                            keychainPassword!,
                        }
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    _logger.LogError($"Failed to unlock Keychain, which is required to install mobile provisioning certificates. 'security unlock-keychain' exited with exit code {exitCode}.");
                    continue;
                }

                // Import certificates and private keys, and allow everything to access them.
                var forImport = new[]
                {
                    mobileProvision.PrivateKeyPasswordlessP12Path!,
                    mobileProvision.PublicKeyPemPath!,
                    mobileProvision.AppleProvidedCertificatePath!,
                };
                foreach (var import in forImport)
                {
                    _logger.LogInformation($"Importing certificate/public key/private key '{import}'...");
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = "/usr/bin/security",
                            Arguments = new LogicalProcessArgument[]
                            {
                                "import",
                                Path.GetFullPath(import),
                                "-k",
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Keychains", "login.keychain"),
                                "-A",
                                "-P",
                                string.Empty,
                            }
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);
                    if (exitCode != 0)
                    {
                        _logger.LogWarning($"Failed to import certificate/public key/private key '{import}' (exit code {exitCode})!");
                    }
                }

                // Import the .mobileprovision file.
                _logger.LogInformation($"Importing mobile provisioning file '{mobileProvision.MobileProvisionPath!}'...");
                var targetFile = Path.Combine(provisioningProfilesRoot, Path.GetFileName(mobileProvision.MobileProvisionPath!));
                if (!File.Exists(targetFile))
                {
                    try
                    {
                        File.Copy(mobileProvision.MobileProvisionPath!, targetFile, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to import mobile provision file '{mobileProvision.MobileProvisionPath!}': {ex}");
                    }
                }
            }
        }
    }
}
