namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class PlivoListMeta
    {
        [JsonPropertyName("limit"), JsonRequired]
        public int Limit { get; set; }

        [JsonPropertyName("next"), JsonRequired]
        public string? Next { get; set; }

        [JsonPropertyName("offset"), JsonRequired]
        public int Offset { get; set; }

        [JsonPropertyName("previous"), JsonRequired]
        public string? Previous { get; set; }

        [JsonPropertyName("total_count"), JsonRequired]
        public int TotalCount { get; set; }
    }
}
