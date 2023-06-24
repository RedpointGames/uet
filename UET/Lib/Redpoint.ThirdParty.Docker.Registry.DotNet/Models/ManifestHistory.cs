namespace Docker.Registry.DotNet.Models
{
    using System.Text.Json.Serialization;

    public class ManifestHistory
    {
        [JsonPropertyName("v1Compatibility")]
        public string V1Compatibility { get; set; }
    }
}