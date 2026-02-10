using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class BuildWebhookJson
    {
        [JsonPropertyName("build_id")]
        public long? Id { get; set; }

        [JsonPropertyName("pipeline_id")]
        public long? PipelineId { get; set; }

        [JsonPropertyName("project_id")]
        public long? ProjectId { get; set; }

        [JsonPropertyName("project_name")]
        public string? ProjectName { get; set; }

        [JsonPropertyName("build_stage")]
        public string? Stage { get; set; }

        [JsonPropertyName("build_name")]
        public string? Name { get; set; }

        [JsonPropertyName("build_status")]
        public string? Status { get; set; }

        [JsonPropertyName("build_created_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("build_started_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? StartedAt { get; set; }

        [JsonPropertyName("build_finished_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? FinishedAt { get; set; }

        [JsonPropertyName("build_duration")]
        public long? Duration { get; set; }

        [JsonPropertyName("build_allow_failure")]
        public bool AllowFailure { get; set; }

        [JsonPropertyName("build_failure_reason")]
        public string? FailureReason { get; set; }

        [JsonPropertyName("user")]
        public UserJson? User { get; set; }

        [JsonPropertyName("commit")]
        public CommitJson? Commit { get; set; }

        [JsonPropertyName("runner")]
        public RunnerJson? Runner { get; set; }
    }
}
