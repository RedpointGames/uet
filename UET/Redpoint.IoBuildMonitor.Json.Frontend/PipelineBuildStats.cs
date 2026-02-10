using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.Frontend
{
    public class PipelineBuildStats
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON API")]
        public string? Url { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("startedUtcMillis")]
        public long? StartedUtcMillis { get; set; }

        [JsonPropertyName("estimatedUtcMillis")]
        public long? EstimatedUtcMillis { get; set; }

        [JsonPropertyName("downstreamPipeline")]
        public PipelineStats? DownstreamPipeline { get; set; }

        [JsonPropertyName("tests")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API")]
        public List<TestStats>? Tests { get; set; }
    }
}
