namespace Redpoint.CloudFramework.CLI
{
    using System.Text.Json.Serialization;

    internal class YarnLogEntry
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("indent")]
        public string? Indent { get; set; }

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }
}
