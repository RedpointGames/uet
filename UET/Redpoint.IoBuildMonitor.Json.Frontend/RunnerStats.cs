namespace Io.Json.Frontend
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class RunnerStats
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("builds")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API")]
        public List<BuildStats>? Builds { get; set; }
    }
}
