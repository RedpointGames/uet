using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.Frontend
{
    public class PipelineStats
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("url")]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON API")]
        public string? Url { get; set; }

        [JsonPropertyName("stages")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API")]
        public List<PipelineStageStats>? Stages { get; set; }

        [JsonPropertyName("startedUtcMillis")]
        public long? StartedUtcMillis { get; set; }

        [JsonPropertyName("estimatedUtcMillis")]
        public long? EstimatedUtcMillis { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
