namespace Docker.Registry.DotNet.Models
{
    using System.Text.Json.Serialization;

    public class ManifestFsLayer
    {
        [JsonPropertyName("blobSum")]
        public string BlobSum { get; set; }
    }
}