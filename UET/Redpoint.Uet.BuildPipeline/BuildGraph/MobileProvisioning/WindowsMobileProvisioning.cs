namespace Redpoint.Uet.BuildPipeline.BuildGraph.MobileProvisioning
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Configuration.Engine;
    using System;
    using System.Collections.Generic;
    using System.Runtime.Versioning;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    internal class WindowsMobileProvisioning : IMobileProvisioning
    {
        private readonly ILogger<MacMobileProvisioning> _logger;

        public WindowsMobileProvisioning(
            ILogger<MacMobileProvisioning> logger)
        {
            _logger = logger;
        }

        public async Task InstallMobileProvisions(
            IEnumerable<BuildConfigMobileProvision> mobileProvisions,
            CancellationToken cancellationToken)
        {
            var provisioningProfilesRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Apple Computer",
                "MobileDevice",
                "Provisioning Profiles");
            Directory.CreateDirectory(provisioningProfilesRoot);

            var store = new X509Store();
            store.Open(OpenFlags.ReadWrite);

            foreach (var mobileProvision in mobileProvisions)
            {
                // Import certificates and private keys, and allow everything to access them.
                var forImport = new[]
                {
                    mobileProvision.PrivateKeyPasswordlessP12Path!,
                };
                foreach (var import in forImport)
                {
                    _logger.LogInformation($"Importing certificate/public key/private key '{import}'...");
                    try
                    {
                        store.Add(X509CertificateLoader.LoadPkcs12FromFile(import, null, X509KeyStorageFlags.Exportable));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to import certificate '{import}': {ex}");
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
