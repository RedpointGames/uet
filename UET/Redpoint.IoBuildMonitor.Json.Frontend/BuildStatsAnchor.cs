namespace Io.Json.Frontend
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildStatsAnchor
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON API")]
        public string? Url { get; set; }

        [JsonPropertyName("startedUtcMillis")]
        public long? StartedUtcMillis { get; set; }

        [JsonPropertyName("estimatedUtcMillis")]
        public long? EstimatedUtcMillis { get; set; }
    }
}
