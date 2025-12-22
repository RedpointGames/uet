namespace Redpoint.KubernetesManager.Manifest
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The bundle which is encrypted in the <see cref="NodeAuthorizeResponse"/>.
    /// </summary>
    public class NodeAuthorizeResponseEncryptedBundle
    {
        [JsonPropertyName("nodePrivateKeyPem")]
        public required string NodePrivateKeyPem { get; set; }

        [JsonPropertyName("nodeCertificatePem")]
        public required string NodeCertificatePem { get; set; }

        [JsonPropertyName("certificateAuthorityPem")]
        public required string CertificateAuthorityPem { get; set; }

        [JsonPropertyName("nodeName")]
        public required string NodeName { get; set; }
    }
}
