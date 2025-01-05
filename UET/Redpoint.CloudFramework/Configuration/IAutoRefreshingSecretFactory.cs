namespace Redpoint.CloudFramework.Configuration
{
    /// <summary>
    /// A factory which can construct <see cref="IAutoRefreshingSecret"/> instances
    /// over Google Cloud Secret Manager secrets.
    /// </summary>
    public interface IAutoRefreshingSecretFactory
    {
        /// <summary>
        /// Create a new instance of <see cref="IAutoRefreshingSecret"/> for the specified 
        /// Google Cloud Secret Manager secret, if it exists. If <paramref name="requireSuccessfulLoad"/>
        /// is true, this method will throw an exception if the secret data can't be read for 
        /// the latest version, otherwise it will return an implementation of 
        /// <see cref="IAutoRefreshingSecret"/> that contains no initial data.
        /// </summary>
        /// <param name="secretName">The name of the secret to load.</param>
        /// <param name="requireSuccessfulLoad">If true, this method will throw an exception.</param>
        /// <returns>The loaded secret.</returns>
        /// <exception cref="SecretManagerSecretFailedToLoadException"><paramref name="requireSuccessfulLoad"/> was set to true and the secret could not be loaded.</exception>
        IAutoRefreshingSecret Create(string secretName, bool requireSuccessfulLoad);
    }
}
