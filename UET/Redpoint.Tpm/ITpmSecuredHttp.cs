namespace Redpoint.Tpm
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    /// <summary>
    /// Service for creating TPM-secured HTTPS clients and servers.
    /// </summary>
    public interface ITpmSecuredHttp
    {
        /// <summary>
        /// Construct an implementation of <see cref="ITpmSecuredHttpServer"/>, which provides methods that can be used with Kestrel and ASP.NET Core to negotiate and validate client certificates with clients.
        /// </summary>
        /// <param name="certificateAuthority">The certificate authority used for signing client certificate signing requests; must include a private component.</param>
        /// <returns>The implementation of <see cref="ITpmSecuredHttpServer"/>.</returns>
        ITpmSecuredHttpServer CreateHttpServer(
            X509Certificate2 certificateAuthority);

        /// <summary>
        /// Construct a <see cref="HttpClient"/> that can be used to call endpoints secured with a <see cref="ITpmSecuredHttpServer"/>. The server will be able to verify the identity of this machine using the attestation identity key in the local machine's TPM.
        /// </summary>
        /// <param name="negotiateUrl">The URL which implements the call to <see cref="ITpmSecuredHttpServer.HandleNegotiationRequestAsync(Microsoft.AspNetCore.Http.HttpContext)"/>. This can be a HTTP or HTTPS endpoint.</param>
        /// <param name="cancellationToken">The cancellation token to cancel negotiation of a client certificate.</param>
        /// <returns>The new <see cref="HttpClient"/>.</returns>
        Task<HttpClient> CreateHttpClientAsync(
            Uri negotiateUrl,
            CancellationToken cancellationToken);
    }
}
