namespace UET.Commands.Internal.RemoteZfsServer
{
    using System.Text.Json.Serialization;

    internal class TrueNasQuery
    {
        [JsonPropertyName("query-filters"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[][]? QueryFilters { get; set; }

        [JsonPropertyName("query-options"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public TrueNasQueryOptions? QueryOptions { get; set; }
    }
}
