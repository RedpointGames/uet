namespace Redpoint.Uet.SdkManagement
{
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformConfigExtractor
    {
        [JsonPropertyName("MsiSourceDirectory"), JsonRequired]
        public string? MsiSourceDirectory { get; set; }

        [JsonPropertyName("MsiFilenameFilter")]
        public string? MsiFilenameFilter { get; set; }

        [JsonPropertyName("ExtractionSubdirectoryPath"), JsonRequired]
        public string? ExtractionSubdirectoryPath { get; set; }
    }
}
