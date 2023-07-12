namespace Redpoint.CredentialDiscovery
{
    /// <summary>
    /// Thrown if no credential could be discovered.
    /// </summary>
    public class UnableToDiscoverCredentialException : Exception
    {
        /// <summary>
        /// Thrown if no credential could be discovered.
        /// </summary>
        public UnableToDiscoverCredentialException(string? message) : base(message)
        {
        }
    }
}