namespace Redpoint.Tpm.Negotiate
{
    using System.Text.Json.Serialization;

    internal sealed class NegotiateCertificateRequest
    {
        [JsonPropertyName("ekTpmPublicBase64")]
        public required string EkTpmPublicBase64 { get; set; }

        [JsonPropertyName("aikTpmPublicBase64")]
        public required string AikTpmPublicBase64 { get; set; }

        [JsonPropertyName("clientCertificateCsrPem")]
        public required string ClientCertificateCsrPem { get; set; }
    }
}
