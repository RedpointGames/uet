namespace Docker.Registry.DotNet.Models
{
    using System.Text.Json.Serialization;

    public class ManifestSignature
    {
        [JsonPropertyName("header")]
        public ManifestSignatureHeader Header { get; set; }

        [JsonPropertyName("signature")]
        public string Signature { get; set; }

        [JsonPropertyName("protected")]
        public string Protected { get; set; }
    }
}