namespace UET.Commands.Internal.RemoteZfsServer
{
    using System.Text.Json.Serialization;

    internal class TrueNasQueryOptions
    {
        [JsonPropertyName("extra"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? Extra { get; set; }

        [JsonPropertyName("order_by"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? OrderBy { get; set; }

        [JsonPropertyName("limit"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Limit { get; set; }
    }
}
