namespace Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk
{
    using System.Text.Json.Serialization;

    class VisualStudioManifestPackageDependency
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("behaviors")]
        public string? Behaviours { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("when")]
        public string[]? When { get; set; }
    }
}
