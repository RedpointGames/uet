namespace Redpoint.Uet.SdkManagement
{
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformConfigStep
    {
        [JsonPropertyName("Installer")]
        public ConfidentialPlatformConfigInstaller? Installer { get; set; }

        [JsonPropertyName("Extractor")]
        public ConfidentialPlatformConfigExtractor? Extractor { get; set; }
    }
}
