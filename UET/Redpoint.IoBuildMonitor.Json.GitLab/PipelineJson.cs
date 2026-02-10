using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class PipelineJson
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        [JsonPropertyName("tag")]
        public bool? Tag { get; set; }

        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("before_sha")]
        public string? PreviousSha { get; set; }

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("stages")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "JSON API")]
        public string[] Stages { get; set; } = [];

        [JsonPropertyName("created_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("finished_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? FinishedAt { get; set; }

        [JsonPropertyName("duration")]
        public long? Duration { get; set; }

        [JsonPropertyName("queued_duration")]
        public long? QueuedDuration { get; set; }
    }
}
