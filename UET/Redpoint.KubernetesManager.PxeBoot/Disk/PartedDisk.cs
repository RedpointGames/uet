namespace Redpoint.KubernetesManager.PxeBoot.Disk
{
    using System.Text.Json.Serialization;

    internal class PartedDisk
    {
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("size")]
        public string? Size { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("transport")]
        public string? Transport { get; set; }

        [JsonPropertyName("logical-sector-size")]
        public int? LogicalSectorSize { get; set; }

        [JsonPropertyName("physical-sector-size")]
        public int? PhysicalSectorSize { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }

        [JsonPropertyName("max-partitions")]
        public int? MaxPartitions { get; set; }

        [JsonPropertyName("partitions")]
        public PartedDiskPartition?[]? Partitions { get; set; }
    }
}
