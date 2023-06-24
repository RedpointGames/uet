namespace Docker.Registry.DotNet.Models
{
    using System.Text.Json.Serialization;

    public abstract class ImageManifest
    {
        /// <summary>
        /// This field specifies the image manifest schema version as an integer.
        /// </summary>
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }
    }
}