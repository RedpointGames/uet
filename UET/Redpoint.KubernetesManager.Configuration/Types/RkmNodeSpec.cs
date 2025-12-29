namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class RkmNodeSpec
    {
        [JsonPropertyName("nodeName")]
        public string? NodeName { get; set; }

        [JsonPropertyName("nodeGroup")]
        public string? NodeGroup { get; set; }

        [JsonPropertyName("authorized")]
        public bool Authorized { get; set; }

        [JsonPropertyName("forceReprovision")]
        public bool ForceReprovision { get; set; }

        [JsonPropertyName("inactiveBootEntries")]
        [SuppressMessage("Design", "CA1819", Justification = "Used for JSON serialization.")]
        public string?[]? InactiveBootEntries { get; set; }
    }
}
