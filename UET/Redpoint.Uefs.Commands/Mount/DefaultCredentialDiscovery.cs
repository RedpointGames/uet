namespace Redpoint.Uefs.Commands.Mount
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.ContainerRegistry;
    using Redpoint.Uefs.Protocol;
    using System;
    using System.Text.RegularExpressions;

    internal class DefaultCredentialDiscovery : ICredentialDiscovery
    {
        private readonly ILogger<DefaultCredentialDiscovery> _logger;

        public DefaultCredentialDiscovery(
            ILogger<DefaultCredentialDiscovery> logger)
        {
            _logger = logger;
        }

        public GitCredential GetGitCredential(string gitUrl)
        {
            var publicKeyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_rsa.pub");
            var privateKeyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_rsa");

            var shortSshUrlRegex = new Regex("^(.+@)*([\\w\\d\\.]+):(.*)$");
            var shortSshUrlMatch = shortSshUrlRegex.Match(gitUrl);
            if (shortSshUrlMatch.Success)
            {
                gitUrl = $"ssh://{shortSshUrlMatch.Groups[1].Value}{shortSshUrlMatch.Groups[2].Value}/{shortSshUrlMatch.Groups[3].Value}";
            }
            if (!gitUrl.ToLowerInvariant().StartsWith("ssh://"))
            {
                throw new InvalidOperationException("only SSH URLs are supported (as the daemon can not ask for username and password interactively for HTTPS)");
            }

            if (!File.Exists(publicKeyFile))
            {
                throw new InvalidOperationException($"expected the public key file to exist: {publicKeyFile}");
            }
            if (!File.Exists(privateKeyFile))
            {
                throw new InvalidOperationException($"expected the private key file to exist: {privateKeyFile}");
            }

            return new GitCredential
            {
                SshPublicKeyAsPem = File.ReadAllText(publicKeyFile),
                SshPrivateKeyAsPem = File.ReadAllText(privateKeyFile),
            };
        }

        public Protocol.RegistryCredential GetRegistryCredential(string packageTag)
        {
            var registryRegex = RegistryTagRegex.Regex.Match(packageTag);
            if (registryRegex == null)
            {
                throw new InvalidOperationException("registry tag did not match expected syntax!");
            }
            var registryHost = registryRegex.Groups["host"].Value;
            var registryCredential = RegistryClientFactory.GetRegistryCredential(registryHost);
            var grpcRegistryCredential = new Protocol.RegistryCredential();
            if (registryCredential != null)
            {
                grpcRegistryCredential.Username = registryCredential.Username;
                grpcRegistryCredential.Password = registryCredential.Password;
            }
            return grpcRegistryCredential;
        }
    }
}
