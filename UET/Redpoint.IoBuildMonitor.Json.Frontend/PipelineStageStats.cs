using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Io.Json.Frontend
{
    public class PipelineStageStats
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("builds")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API")]
        public List<PipelineBuildStats>? Builds { get; set; }
    }
}
