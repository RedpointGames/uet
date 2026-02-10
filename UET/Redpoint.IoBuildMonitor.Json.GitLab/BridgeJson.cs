using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.GitLab
{
    public class BridgeJson
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("stage")]
        public string? Stage { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        [JsonPropertyName("tag")]
        public bool? Tag { get; set; }

        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("pipeline")]
        public BridgePipelineJson? Pipeline { get; set; }

        [JsonPropertyName("downstream_pipeline")]
        public BridgePipelineJson? DownstreamPipeline { get; set; }
    }
}
