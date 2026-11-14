namespace Redpoint.Vfs.Client
{
    using System.Text.Json.Serialization;

    internal class WindowsJsonRequest
    {
        [JsonPropertyName("mount")]
        public bool Mount { get; set; }

        [JsonPropertyName("sourceUri")]
        public string? SourceUri { get; set; }

        [JsonPropertyName("mountPath")]
        public string? MountPath { get; set; }
    }
}
