namespace Redpoint.Uet.SdkManagement
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformConfig
    {
        [JsonPropertyName("Version"), JsonRequired]
        public string? Version { get; set; }

        [JsonPropertyName("CommonPlatformName")]
        public string? CommonPlatformName { get; set; }

        [JsonPropertyName("Installers")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public ConfidentialPlatformConfigInstaller[]? Installers { get; set; }

        [JsonPropertyName("Extractors")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public ConfidentialPlatformConfigExtractor[]? Extractors { get; set; }

        [JsonPropertyName("EnvironmentVariables")]
        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        [JsonPropertyName("AutoSdkRelativePathMappings")]
        public Dictionary<string, string>? AutoSdkRelativePathMappings { get; set; }

        [JsonPropertyName("AutoSdkSetupScripts")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public ConfidentialPlatformAutoSdkSetupScript[]? AutoSdkSetupScripts { get; set; }

        [JsonPropertyName("RequiredWindowsSdk")]
        public ConfidentialPlatformRequiredWindowsSdk? RequiredWindowsSdk { get; set; }
    }
}
