using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.Frontend
{
    public class HistoryStats
    {
        [JsonPropertyName("recentPipelines")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API")]
        public List<PipelineStats>? RecentPipelines { get; set; }
    }
}
