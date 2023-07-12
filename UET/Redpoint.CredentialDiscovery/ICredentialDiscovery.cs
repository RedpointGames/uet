namespace Redpoint.CredentialDiscovery
{
    using Redpoint.Uefs.Protocol;

    /// <summary>
    /// Provides APIs for discovering credentials.
    /// </summary>
    public interface ICredentialDiscovery
    {
        /// <summary>
        /// Discover the Git credential for the specified URL.
        /// </summary>
        /// <param name="repositoryUrl">The Git URL to discover a credential for.</param>
        /// <returns>The Git credential to use when accessing this repository URL.</returns>
        /// <exception cref="UnableToDiscoverCredentialException">Thrown if no credential can be discovered to access this URL.</exception>
        GitCredential GetGitCredential(string repositoryUrl);

        /// <summary>
        /// Discover the container registry credential for the specified URL.
        /// </summary>
        /// <param name="containerRegistryTag">The container registry tag to discover a credential for.</param>
        /// <returns>The container registry credential to use when accessing this repository URL.</returns>
        /// <exception cref="UnableToDiscoverCredentialException">Thrown if no credential can be discovered to access this container tag.</exception>
        RegistryCredential GetRegistryCredential(string containerRegistryTag);
    }
}