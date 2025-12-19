namespace Redpoint.KubernetesManager.Services
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using System;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    internal class DefaultEncryptionConfigManager : IEncryptionConfigManager
    {
        private readonly ILogger<DefaultEncryptionConfigManager> _logger;
        private readonly IPathProvider _pathProvider;

        public DefaultEncryptionConfigManager(
            ILogger<DefaultEncryptionConfigManager> logger,
            IPathProvider pathProvider)
        {
            _logger = logger;
            _pathProvider = pathProvider;
        }

        public string EncryptionConfigPath => Path.Combine(_pathProvider.RKMRoot, "secrets", "encryption-config.yaml");

        public async Task Initialize()
        {
            var encryptionConfigPath = Path.Combine(_pathProvider.RKMRoot, "secrets", "encryption-config.yaml");
            if (!File.Exists(encryptionConfigPath))
            {
                _logger.LogInformation("Generating encryption configuration file...");
                Directory.CreateDirectory(Path.GetDirectoryName(encryptionConfigPath)!);
                var generatedSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                await File.WriteAllTextAsync(encryptionConfigPath, $@"
kind: EncryptionConfig
apiVersion: v1
resources:
  - resources:
      - secrets
    providers:
      - aescbc:
          keys:
            - name: key1
              secret: {generatedSecret}
      - identity: {{}}
".Trim());
            }
            else
            {
                _logger.LogInformation("Encryption configuration file already exists.");
            }
        }
    }
}
