namespace Redpoint.SdkManagement.WindowsSdk
{
    using System.Text.Json.Serialization;

    class VisualStudioManifest
    {
        [JsonPropertyName("channelItems")]
        public VisualStudioManifestChannelItem[]? ChannelItems { get; set; }

        [JsonPropertyName("packages")]
        public VisualStudioManifestChannelItem[]? Packages { get; set; }
    }
}
