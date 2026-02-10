using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class PipelineWebhookJson
    {
        [JsonPropertyName("object_attributes")]
        public PipelineJson? ObjectAttributes { get; set; }

        [JsonPropertyName("merge_request")]
        public MergeRequestJson? MergeRequest { get; set; }

        [JsonPropertyName("user")]
        public UserJson? User { get; set; }

        [JsonPropertyName("project")]
        public ProjectJson? Project { get; set; }

        [JsonPropertyName("commit")]
        public CommitJson? Commit { get; set; }

        [JsonPropertyName("builds")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "JSON API")]
        public BuildJson[] Builds { get; set; } = [];
    }
}
