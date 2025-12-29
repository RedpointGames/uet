namespace Redpoint.KubernetesManager.PxeBoot.Disk
{
    using System.Text.Json.Serialization;

    internal class PartedDiskPartition
    {
        [JsonPropertyName("number")]
        public int? Number { get; set; }

        [JsonPropertyName("start")]
        public string? Start { get; set; }

        [JsonPropertyName("end")]
        public string? End { get; set; }

        [JsonPropertyName("size")]
        public string? Size { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("type-uuid")]
        public string? TypeUuid { get; set; }

        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("flags")]
        public string?[]? Flags { get; set; }
    }
}
