namespace Redpoint.KubernetesManager.Manifest
{
    using System.Text.Json.Serialization;

    public class ActiveDirectoryManifest
    {
        [JsonPropertyName("domain")]
        public required string Domain { get; set; }
    }
}
