namespace Docker.Registry.DotNet.Models
{
    using System.Text.Json.Serialization;

    public class Catalog
    {
        [JsonPropertyName("repositories")]
        public string[] Repositories { get; set; }
    }
}