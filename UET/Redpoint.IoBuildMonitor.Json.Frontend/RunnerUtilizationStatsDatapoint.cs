using System.Text.Json.Serialization;

namespace Io.Json.Frontend
{
    public class RunnerUtilizationStatsDatapoint
    {
        [JsonPropertyName("m")]
        public long? TimestampMinute { get; set; }

        [JsonPropertyName("c")]
        public long? Created { get; set; }

        [JsonPropertyName("p")]
        public long? Pending { get; set; }

        [JsonPropertyName("r")]
        public long? Running { get; set; }
    }
}
