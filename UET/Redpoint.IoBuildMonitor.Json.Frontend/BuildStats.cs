namespace Io.Json.Frontend
{
    using System.Text.Json.Serialization;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    public class BuildStats
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("anchors")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "JSON API.")]
        public List<BuildStatsAnchor>? Anchors { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
