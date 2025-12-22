namespace Redpoint.KubernetesManager.Manifest
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Used by the controller to send an authorized node's private key to the node.
    /// </summary>
    public class NodeAuthorizeResponse
    {
        [JsonPropertyName("envelopingKeyBase64")]
        public required string EnvelopingKeyBase64 { get; set; }

        [JsonPropertyName("encryptedBundleBase64")]
        public required string EncryptedBundleBase64 { get; set; }
    }
}
