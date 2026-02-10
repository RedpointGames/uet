using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.Frontend
{
    public class RunnerUtilizationStats
    {
        [JsonPropertyName("tag")]
        public string? Tag { get; set; } = string.Empty;

        [JsonPropertyName("capacity")]
        public long Capacity { get; set; } = 0;

        [JsonPropertyName("datapoints")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API")]
        public List<RunnerUtilizationStatsDatapoint>? Datapoints { get; set; }

        [JsonPropertyName("desiredCapacity")]
        public long DesiredCapacity { get; set; } = 0;

        [JsonPropertyName("desiredCapacityDistribution")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "JSON API")]
        public RunnerUtilizationStatsCapacityDistribution[] DesiredCapacityDistribution { get; set; } = Array.Empty<RunnerUtilizationStatsCapacityDistribution>();
    }
}
