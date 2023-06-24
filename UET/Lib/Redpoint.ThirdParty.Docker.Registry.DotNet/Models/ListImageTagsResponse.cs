namespace Docker.Registry.DotNet.Models
{
    using System.Text.Json.Serialization;

    public class ListImageTagsResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("tags")]
        public string[] Tags { get; set; }
    }
}