namespace Redpoint.Uefs.Daemon.PackageFs.CachingStorage
{
    using System.Text.Json.Serialization;

    internal class CachingInfoJson
    {
        [JsonPropertyName("type")]
        public string Type = string.Empty;

        [JsonPropertyName("serializedObject")]
        public string SerializedObject = "{}";

        [JsonPropertyName("length")]
        public long Length = 0;
    }
}
