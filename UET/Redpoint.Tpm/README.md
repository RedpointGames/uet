# Redpoint.Tpm

Provides APIs for securing HTTPS communications using attestation identity keys stored
in a TPM. This can be used by servers to ensure that the client connecting to them is
the machine they claim to be.

## Example

On the server, assuming that you already have a certificate authority that can be used for generating client certificates:

```csharp
// Create the API that allows you to secure HTTPS with TPM attested certificates.
_tpmSecuredHttpServer = tpmSecuredHttpService.CreateHttpServer(certificateAuthority);

// Configure Kestrel's HTTPS options...
kestrelOptions.ListenLocalhost(8791, options =>
{
    options.UseHttps(https =>
    {
        _tpmSecuredHttpServer.ConfigureHttps(https);
    });
});

// In a negotiation endpoint, handle the negotiation...
if (httpContext.Request.Path == "/negotiate")
{
    await _tpmSecuredHttpServer!.HandleNegotiationRequestAsync(httpContext);
}

// In an endpoint you want to verify the identity of the connecting client...
var pem = await _tpmSecuredHttpServer!.GetAikPemVerifiedByClientCertificateAsync(httpContext);

// 'pem' will contain the PEM of the attestation identity key.
```

On the client:

```csharp
// Create the client; the URI should be that of the negotiation endpoint of the server.
var client = await tpmSecuredHttpService.CreateHttpClientAsync(
    new Uri("http://127.0.0.1:8790/negotiate"),
    cancellationToken);

// Use the client to call HTTPS endpoints. The server in the /test endpoint should call
// GetAikPemVerifiedByClientCertificateAsync to get the PEM and verify the client.
var result = await client.GetStringAsync(
    new Uri("https://127.0.0.1:8791/test"),
    cancellationToken);
```