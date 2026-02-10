using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class RunnerJson
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("active")]
        public bool? Active { get; set; }

        [JsonPropertyName("runner_type")]
        public string? RunnerType { get; set; }

        [JsonPropertyName("is_shared")]
        public bool? IsShared { get; set; }

        [JsonPropertyName("tags")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "JSON API")]
        public string[] Tags { get; set; } = [];
    }
}
