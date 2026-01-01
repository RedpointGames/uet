namespace Redpoint.Tpm
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using System.Threading.Tasks;

    /// <summary>
    /// Constructed by calling <see cref="ITpmSecuredHttp.CreateHttpServer(System.Security.Cryptography.X509Certificates.X509Certificate2)"/>.
    /// </summary>
    public interface ITpmSecuredHttpServer
    {
        /// <summary>
        /// Configure the HTTPS options inside <see cref="ListenOptionsHttpsExtensions.UseHttps(ListenOptions)"/> so that client certificates will be verified against the certificate authority.
        /// </summary>
        /// <param name="options">The <see cref="HttpsConnectionAdapterOptions"/> to configure.</param>
        void ConfigureHttps(HttpsConnectionAdapterOptions options);

        /// <summary>
        /// Handle the current request and negotiate a client certificate with the client in such a manner that only the client's TPM can decrypt the signed client certificate attesting the TPM's identity.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContent"/> of the current request.</param>
        /// <exception cref="InvalidNegotiationRequestException">The negotiation request was invalid.</exception>
        /// <returns>The asynchronous task to await.</returns>
        Task HandleNegotiationRequestAsync(
            HttpContext httpContext);

        /// <summary>
        /// Gets the AIK PEM verified by the client certificate on the request. This function throws if the HTTPS request can't be verified or is otherwise invalid, so once it returns, you can be certain that the caller is who they claim to be.
        /// </summary>
        /// <returns>An asynchronous task which returns the PEM of the TPM's AIK.</returns>
        /// <exception cref="RequestValidationFailedException">Thrown if the security of the request can't be verified.</exception>
        Task<string> GetAikPemVerifiedByClientCertificateAsync(
            HttpContext httpContext);
    }
}
