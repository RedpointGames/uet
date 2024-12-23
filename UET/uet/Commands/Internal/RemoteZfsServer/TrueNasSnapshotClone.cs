namespace UET.Commands.Internal.RemoteZfsServer
{
    using System.Text.Json.Serialization;

    internal class TrueNasSnapshotClone
    {
        [JsonPropertyName("snapshot"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Snapshot { get; set; }

        [JsonPropertyName("dataset_dst"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DatasetDest { get; set; }
    }
}
