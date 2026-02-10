using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class BuildJson
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("stage")]
        public string? Stage { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("created_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("started_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? StartedAt { get; set; }

        [JsonPropertyName("finished_at")]
        [JsonConverter(typeof(GitLabDateConverter))]
        public DateTimeOffset? FinishedAt { get; set; }

        [JsonPropertyName("duration")]
        public long? Duration { get; set; }

        [JsonPropertyName("when")]
        public string? When { get; set; }

        [JsonPropertyName("manual")]
        public bool? Manual { get; set; }

        [JsonPropertyName("allow_failure")]
        public bool? AllowFailure { get; set; }

        [JsonPropertyName("user")]
        public UserJson? User { get; set; }

        [JsonPropertyName("runner")]
        public RunnerJson? Runner { get; set; }

        [JsonPropertyName("artifacts_file")]
        public ArtifactsFileInfo? ArtifactsFile { get; set; }
    }
}
