namespace UET.Commands.Internal.RemoteZfsServer
{
    using System.Text.Json.Serialization;

    internal class TrueNasSnapshot
    {
        [JsonPropertyName("pool")]
        public required string Pool { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("snapshot_name")]
        public required string SnapshotName { get; set; }

        [JsonPropertyName("dataset")]
        public required string Dataset { get; set; }

        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("createtxg")]
        public required string CreateTxg { get; set; }
    }
}
