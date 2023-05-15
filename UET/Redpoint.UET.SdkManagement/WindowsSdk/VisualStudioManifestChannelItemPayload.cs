namespace Redpoint.UET.SdkManagement.WindowsSdk
{
    using System.Text.Json.Serialization;

    class VisualStudioManifestChannelItemPayload
    {
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
