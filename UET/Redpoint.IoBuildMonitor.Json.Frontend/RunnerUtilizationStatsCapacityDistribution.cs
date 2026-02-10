using System.Text.Json.Serialization;

namespace Io.Json.Frontend
{
    public class RunnerUtilizationStatsCapacityDistribution
    {
        [JsonPropertyName("percentile")]
        public double Percentile { get; set; }

        [JsonPropertyName("desiredCapacity")]
        public double DesiredCapacity { get; set; }
    }
}
