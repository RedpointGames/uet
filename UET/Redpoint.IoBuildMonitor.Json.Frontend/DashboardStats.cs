using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Io.Json.Frontend
{
    public class DashboardStats
    {
        [JsonPropertyName("pendingPipelineCount")]
        public long? PendingPipelineCount { get; set; }

        [JsonPropertyName("pendingBuildCount")]
        public long? PendingBuildCount { get; set; }

        [JsonPropertyName("runners")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API.")]
        public List<RunnerStats>? Runners { get; set; }

        [JsonPropertyName("pendingPipelines")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API.")]
        public List<PipelineStats>? PendingPipelines { get; set; }
    }
}
