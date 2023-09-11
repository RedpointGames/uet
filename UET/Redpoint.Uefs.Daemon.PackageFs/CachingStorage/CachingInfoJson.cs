namespace Redpoint.Uefs.Daemon.PackageFs.CachingStorage
{
    using System.Text.Json.Serialization;

    internal sealed class CachingInfoJson
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("serializedObject")]
        public string SerializedObject { get; set; } = "{}";

        [JsonPropertyName("length")]
        public long Length { get; set; } = 0;
    }
}
