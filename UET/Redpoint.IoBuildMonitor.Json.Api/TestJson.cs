namespace Io.Json.Api
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class TestJson
    {
        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        [JsonPropertyName("gauntletInstance")]
        public string? GauntletInstance { get; set; }

        [JsonPropertyName("automationInstance")]
        public string? AutomationInstance { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("gauntlet")]
        public bool? IsGauntlet { get; set; }

        [JsonPropertyName("dateStartedUtc")]
        public long? DateStartedUtc { get; set; }

        [JsonPropertyName("dateFinishedUtc")]
        public long? DateFinishedUtc { get; set; }

        [JsonPropertyName("durationSeconds")]
        public double? DurationSeconds { get; set; }

        [JsonPropertyName("appendPrimaryLogLines")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "JSON API.")]
        public string[] AppendPrimaryLogLines { get; set; } = [];

        [JsonPropertyName("appendAdditionalLogLines")]
        public Dictionary<string, string[]> AppendAdditionalLogLines { get; set; } = new Dictionary<string, string[]>();
    }
}
