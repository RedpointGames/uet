namespace Redpoint.UET.Workspace.Credential
{
    using System;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Redpoint.ThirdParty.CredentialManagement;
    using Redpoint.Uefs.Protocol;

    internal class DefaultCredentialManager : ICredentialManager
    {
        public GitCredential GetGitCredentialForRepositoryUrl(string repositoryUrl)
        {
            var publicKeyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_rsa.pub");
            var privateKeyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_rsa");

            var shortSshUrlRegex = new Regex("^(.+@)*([\\w\\d\\.]+):(.*)$");
            var shortSshUrlMatch = shortSshUrlRegex.Match(repositoryUrl);
            if (shortSshUrlMatch.Success)
            {
                repositoryUrl = $"ssh://{shortSshUrlMatch.Groups[1].Value}{shortSshUrlMatch.Groups[2].Value}/{shortSshUrlMatch.Groups[3].Value}";
            }
            if (!repositoryUrl.ToLowerInvariant().StartsWith("ssh://"))
            {
                throw new InvalidOperationException("Only SSH URLs are supported (as the daemon can not ask for username and password interactively for HTTPS)");
            }

            if (!File.Exists(publicKeyFile))
            {
                throw new InvalidOperationException($"Expected the public key file to exist: {publicKeyFile}");
            }
            if (!File.Exists(privateKeyFile))
            {
                throw new InvalidOperationException($"Expected the public key file to exist: {privateKeyFile}");
            }

            return new GitCredential
            {
                SshPublicKeyAsPem = File.ReadAllText(publicKeyFile).Trim(),
                SshPrivateKeyAsPem = File.ReadAllText(privateKeyFile).Trim(),
            };
        }

        public static readonly Regex TagRegex = new Regex("^(?<host>[a-z\\.\\:0-9]+)/(?<path>[a-z\\./0-9-_]+):?(?<label>[a-z\\.0-9-_]+)?$");

        public RegistryCredential GetRegistryCredentialForTag(string rawTag)
        {
            var tag = TagRegex.Match(rawTag);
            if (!tag.Success)
            {
                throw new InvalidOperationException("Invalid package URL");
            }
            var host = tag.Groups["host"].Value;

            var ciRegistry = Environment.GetEnvironmentVariable("CI_REGISTRY");
            var ciRegistryUser = Environment.GetEnvironmentVariable("CI_REGISTRY_USER");
            var ciRegistryPassword = Environment.GetEnvironmentVariable("CI_REGISTRY_PASSWORD");

            if (!string.IsNullOrWhiteSpace(ciRegistry) && !string.IsNullOrWhiteSpace(ciRegistryUser) && !string.IsNullOrWhiteSpace(ciRegistryPassword))
            {
                if (string.Compare(ciRegistry, host, true) == 0)
                {
                    // If the GitLab CI variables are for this registry host, just use the environment variables directly.
                    // This saves you from having to run `docker login` (which is known to have issues if run concurrently
                    // on the same machine).
                    return new RegistryCredential
                    {
                        Username = ciRegistryUser,
                        Password = ciRegistryPassword,
                    };
                }
            }

            var dockerJsonPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".docker",
                "config.json");
            if (!File.Exists(dockerJsonPath))
            {
                throw new InvalidOperationException("Missing Docker CLI configuration, which is necessary to authenticate with package registries.");
            }
            var dockerConfig = JsonSerializer.Deserialize<DockerConfigJson>(File.ReadAllText(dockerJsonPath), DockerConfigJsonSourceGenerationContext.Default.DockerConfigJson);

            if (dockerConfig?.CredsStore == "wincred")
            {
                var credential = new Credential
                {
                    Target = host
                };
                if (!credential.Load())
                {
                    throw new InvalidOperationException($"Unable to access the credential stored in the Windows Credential Manager for '{host}'.");
                }
                var password = Encoding.UTF8.GetString(Encoding.Unicode.GetBytes(credential.Password));
                return new RegistryCredential
                {
                    Username = credential.Username,
                    Password = password,
                };
            }
            else if (dockerConfig?.Auths?.ContainsKey(host) ?? false)
            {
                var basicAuth = Encoding.UTF8.GetString(Convert.FromBase64String(dockerConfig.Auths[host].Auth)).Split(":", 2);
                return new RegistryCredential
                {
                    Username = basicAuth[0],
                    Password = basicAuth[1],
                };
            }
            else
            {
                throw new InvalidOperationException("Unsupported credential storage type, or we don't have credentials for this registry.");
            }
        }
    }
}
