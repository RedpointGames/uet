using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.Frontend
{
    public class ProjectHealthStats
    {
        [JsonPropertyName("projectId")]
        public long? ProjectId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("webUrl")]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON API")]
        public string? WebUrl { get; set; }

        [JsonPropertyName("defaultBranch")]
        public string? DefaultBranch { get; set; }

        [JsonPropertyName("pipelineId")]
        public long? PipelineId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }
}
