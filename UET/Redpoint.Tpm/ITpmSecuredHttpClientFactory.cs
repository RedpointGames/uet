namespace Redpoint.Tpm
{
    /// <summary>
    /// Used to construct <see cref="HttpClient"/> instances that can perform HTTPS calls secured by a TPM. An instance
    /// of this interface must be obtained from <see cref="ITpmSecuredHttp"/>
    /// </summary>
    public interface ITpmSecuredHttpClientFactory
    {
        /// <summary>
        /// Construct a default <see cref="HttpClient"/> with the client certificate and server certificate validation configured.
        /// </summary>
        /// <returns>The new <see cref="HttpClient"/>.</returns>
        HttpClient Create();

        /// <summary>
        /// Construct a <see cref="HttpClient"/> using the <paramref name="handler"/>. The <paramref name="handler"/> will be modified prior to HTTP client construction to have the client certificate and server certificate validation configured.
        /// </summary>
        /// <returns>The new <see cref="HttpClient"/>.</returns>
        HttpClient Create(HttpClientHandler handler);
    }
}
