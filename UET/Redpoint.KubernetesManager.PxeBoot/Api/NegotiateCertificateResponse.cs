namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using System.Text.Json.Serialization;

    internal class NegotiateCertificateResponse
    {
        [JsonPropertyName("nodeName")]
        public required string NodeName { get; set; }

        [JsonPropertyName("clientCertificateSignedPem")]
        public required string ClientCertificateSignedPem { get; set; }
    }
}
