using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Io.Json.Frontend
{
    public class UtilizationStats
    {
        [JsonPropertyName("runnerUtilizationStats")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API")]
        public List<RunnerUtilizationStats>? RunnerUtilizationStats { get; set; }
    }
}
