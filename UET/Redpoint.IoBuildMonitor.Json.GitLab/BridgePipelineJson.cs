using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class BridgePipelineJson
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("project_id")]
        public long? ProjectId { get; set; }

        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("created_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
