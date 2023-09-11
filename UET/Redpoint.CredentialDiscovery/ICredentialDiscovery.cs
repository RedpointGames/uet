namespace Redpoint.CredentialDiscovery
{
    using Redpoint.Uefs.Protocol;
    using System.Diagnostics.CodeAnalysis;

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
        [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Git URLs can be of a form that is not compatible with the Uri object.")]
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