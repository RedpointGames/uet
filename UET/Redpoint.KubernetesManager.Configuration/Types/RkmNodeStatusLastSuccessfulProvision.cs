namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Text.Json.Serialization;

    public class RkmNodeStatusLastSuccessfulProvision
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("hash")]
        public string? Hash { get; set; }
    }
}
