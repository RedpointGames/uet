namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class RkmNodeGroupActiveDirectory
    {
        [JsonPropertyName("domain")]
        public string? Domain { get; set; }

        [JsonPropertyName("join")]
        public bool Join { get; set; }

        [JsonPropertyName("computerGroups")]
        public IList<string?>? ComputerGroups { get; set; }
    }
}
