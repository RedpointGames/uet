namespace Redpoint.Uet.Automation.TestNotification.Io
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal class IoChange
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
        public bool IsGauntlet { get; set; }

        [JsonPropertyName("dateStartedUtc")]
        public long? DateStartedUtc { get; set; }

        [JsonPropertyName("dateFinishedUtc")]
        public long? DateFinishedUtc { get; set; }

        [JsonPropertyName("durationSeconds")]
        public double? DurationSeconds { get; set; }

        [JsonPropertyName("appendPrimaryLogLines")]
        public string[] AppendPrimaryLogLines { get; set; } = Array.Empty<string>();

        [JsonPropertyName("appendAdditionalLogLines")]
        public Dictionary<string, string[]> AppendAdditionalLogLines { get; set; } = new Dictionary<string, string[]>();
    }
}
