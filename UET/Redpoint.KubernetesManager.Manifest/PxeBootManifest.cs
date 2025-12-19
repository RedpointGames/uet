namespace Redpoint.KubernetesManager.Manifest
{
    using System.Text.Json.Serialization;

    public class PxeBootManifest
    {
        [JsonPropertyName("activeDirectory")]
        public ActiveDirectoryManifest? ActiveDirectoryManifest { get; set; }

        [JsonPropertyName("platform")]
        public required string Platform { get; set; }
    }
}
