namespace Redpoint.Uet.SdkManagement
{
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformConfig
    {
        [JsonPropertyName("Version"), JsonRequired]
        public string? Version { get; set; }

        [JsonPropertyName("CommonPlatformName")]
        public string? CommonPlatformName { get; set; }

        [JsonPropertyName("Installers")]
        public ConfidentialPlatformConfigInstaller[]? Installers { get; set; }

        [JsonPropertyName("Extractors")]
        public ConfidentialPlatformConfigExtractor[]? Extractors { get; set; }

        [JsonPropertyName("EnvironmentVariables")]
        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        [JsonPropertyName("AutoSdkRelativePathMappings")]
        public Dictionary<string, string>? AutoSdkRelativePathMappings { get; set; }

        [JsonPropertyName("AutoSdkSetupScripts")]
        public ConfidentialPlatformAutoSdkSetupScript[]? AutoSdkSetupScripts { get; set; }

        [JsonPropertyName("RequiredWindowsSdk")]
        public ConfidentialPlatformRequiredWindowsSdk? RequiredWindowsSdk { get; set; }
    }
}
