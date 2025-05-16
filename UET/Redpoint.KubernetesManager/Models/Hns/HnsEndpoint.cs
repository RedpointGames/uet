namespace Redpoint.KubernetesManager.Models.Hns
{
    using System.Text.Json.Serialization;

    internal class HnsEndpoint
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; } = null;

        [JsonPropertyName("IPAddress")]
        public string? IPAddress { get; set; } = null;
    }
}
