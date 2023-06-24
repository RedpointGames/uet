using Docker.Registry.DotNet;
using Docker.Registry.DotNet.Authentication;
using Docker.Registry.DotNet.Registry;
using Redpoint.ThirdParty.CredentialManagement;
using System.Text;
using System.Text.Json;

namespace Redpoint.Uefs.ContainerRegistry
{
    /// <summary>
    /// Provides helper functions for obtaining registry credentials and constructing authenticated <see cref="IRegistryClient"/> instances.
    /// </summary>
    public static class RegistryClientFactory
    {
        /// <summary>
        /// Obtain the registry credentials for authenticating with the given Docker registry. This supports reading Docker registry credentials from:
        /// - The CI_REGISTRY_USER/CI_REGISTRY_PASSWORD environment variables, if CI_REGISTRY matches the provided host.
        /// - The ~/.docker/config.json file, and
        /// - The Windows Credential Store.
        /// </summary>
        /// <param name="host">The Docker registry host.</param>
        /// <returns>The credentials for the registry, if there are any.</returns>
        /// <exception cref="InvalidOperationException">Thrown if CI_REGISTRY_USER/CI_REGISTRY_PASSWORD is not set and ~/.docker/config.json is missing.</exception>
        public static RegistryCredential? GetRegistryCredential(string host)
        {
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
            var dockerConfig = JsonSerializer.Deserialize(
                File.ReadAllText(dockerJsonPath),
                UefsRegistryJsonSerializerContext.Default.DockerConfigJson);

            if (dockerConfig?.CredsStore == "wincred")
            {
                var credential = new Credential
                {
                    Target = host
                };
                if (!credential.Load())
                {
                    return null;
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
                var basicAuth = Encoding.UTF8.GetString(Convert.FromBase64String(dockerConfig.Auths[host].Auth!)).Split(":", 2);
                return new RegistryCredential
                {
                    Username = basicAuth[0],
                    Password = basicAuth[1],
                };
            }
            else
            {
                throw new InvalidOperationException("Unable to determine type of credential storage, or we don't have credentials for this registry.");
            }
        }

        /// <summary>
        /// Creates an <see cref="IRegistryClient"/> that authenticates with the registry using username/password OAuth. The OAuth realm to authenticate with is derived from the WWW-Authenticate header present on responses from the registry.
        /// </summary>
        /// <param name="host">The Docker registry host.</param>
        /// <param name="credential">The credentials to authenticate with, typically obtained from <see cref="GetRegistryCredential(string)"/>.</param>
        /// <returns>The new <see cref="IRegistryClient"/> instance.</returns>
        public static IRegistryClient GetRegistryClient(string host, RegistryCredential credential)
        {
            var configuration = new RegistryClientConfiguration(host);
            return configuration.CreateClient(new GetPasswordOAuthAuthenticationProvider(credential.Username, credential.Password));
        }
    }
}
