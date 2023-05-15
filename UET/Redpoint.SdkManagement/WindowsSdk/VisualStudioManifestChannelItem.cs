namespace Redpoint.SdkManagement.WindowsSdk
{
    using System.Text.Json.Serialization;

    class VisualStudioManifestChannelItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("payloads")]
        public VisualStudioManifestChannelItemPayload[]? Payloads { get; set; }

        [JsonPropertyName("dependencies")]
        public Dictionary<string, VisualStudioManifestPackageDependency>? Dependencies { get; set; }

        [JsonPropertyName("machineArch")]
        public string? MachineArch { get; set; }

        [JsonPropertyName("msiProperties")]
        public Dictionary<string, string>? MsiProperties { get; set; }
    }
}
