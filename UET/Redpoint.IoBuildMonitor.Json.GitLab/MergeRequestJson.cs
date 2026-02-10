using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class MergeRequestJson
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("iid")]
        public long? InternalId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("source_branch")]
        public string? SourceBranch { get; set; }

        [JsonPropertyName("source_project_id")]
        public long? SourceProjectId { get; set; }

        [JsonPropertyName("target_branch")]
        public string? TargetBranch { get; set; }

        [JsonPropertyName("target_project_id")]
        public long? TargetProjectId { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("merge_status")]
        public string? MergeStatus { get; set; }

        [JsonPropertyName("url")]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON API")]
        public string? Url { get; set; }
    }
}
