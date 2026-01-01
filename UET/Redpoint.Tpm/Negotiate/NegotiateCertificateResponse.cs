namespace Redpoint.Tpm.Negotiate
{
    using System.Text.Json.Serialization;

    internal sealed class NegotiateCertificateResponse
    {
        [JsonPropertyName("envelopingKeyBase64")]
        public required string EnvelopingKeyBase64 { get; set; }

        [JsonPropertyName("encryptedKeyBase64")]
        public required string EncryptedKeyBase64 { get; set; }

        [JsonPropertyName("encryptedBundleJsonBase64")]
        public required string EncryptedBundleJsonBase64 { get; set; }
    }
}
