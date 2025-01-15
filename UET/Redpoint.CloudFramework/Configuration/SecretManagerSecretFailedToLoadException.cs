namespace Redpoint.CloudFramework.Configuration
{
    using System;

    /// <summary>
    /// Thrown at startup when the application is unable to load required configuration from Google Cloud Secret Manager.
    /// </summary>
    public class SecretManagerSecretFailedToLoadException : Exception
    {
        /// <summary>
        /// Constructs a <see cref="SecretManagerSecretFailedToLoadException"/>.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public SecretManagerSecretFailedToLoadException(string message) : base(message)
        {
        }
    }
}
