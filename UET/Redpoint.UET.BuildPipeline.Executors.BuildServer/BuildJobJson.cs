namespace Redpoint.UET.BuildPipeline.Executors.BuildServer
{
    using System.Text.Json.Serialization;

    public class BuildJobJson
    {
        [JsonPropertyName("Engine"), JsonRequired]
        public string Engine { get; set; } = string.Empty;

        [JsonPropertyName("SharedStoragePath"), JsonRequired]
        public string SharedStoragePath { get; set; } = string.Empty;

        [JsonPropertyName("SharedStorageName"), JsonRequired]
        public string SharedStorageName { get; set; } = string.Empty;

        [JsonPropertyName("SdksPath"), JsonRequired]
        public string? SdksPath { get; set; } = null;

        [JsonPropertyName("BuildGraphTarget"), JsonRequired]
        public string BuildGraphTarget { get; set; } = string.Empty;

        [JsonPropertyName("NodeName"), JsonRequired]
        public string NodeName { get; set; } = string.Empty;

        [JsonPropertyName("DistributionName"), JsonRequired]
        public string DistributionName { get; set; } = string.Empty;

        [JsonPropertyName("BuildGraphScriptName"), JsonRequired]
        public string BuildGraphScriptName { get; set; } = string.Empty;

        [JsonPropertyName("PreparationScripts"), JsonRequired]
        public string[] PreparationScripts { get; set; } = new string[0];

        [JsonPropertyName("GlobalEnvironmentVariables"), JsonRequired]
        public Dictionary<string, string> GlobalEnvironmentVariables { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("Settings"), JsonRequired]
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("ProjectFolderName"), JsonRequired]
        public string? ProjectFolderName { get; set; } = null;
    }
}