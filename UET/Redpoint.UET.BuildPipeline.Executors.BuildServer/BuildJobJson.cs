namespace Redpoint.UET.BuildPipeline.Executors.BuildServer
{
    using System.Text.Json.Serialization;

    internal class BuildJobJson
    {
        [JsonPropertyName("Engine"), JsonRequired]
        public string Engine { get; set; } = string.Empty;

        [JsonPropertyName("SharedStoragePath"), JsonRequired]
        public string SharedStoragePath { get; set; } = string.Empty;

        [JsonPropertyName("SharedStorageName"), JsonRequired]
        public string SharedStorageName { get; set; } = string.Empty;

        [JsonPropertyName("NodeName"), JsonRequired]
        public string NodeName { get; set; } = string.Empty;

        [JsonPropertyName("BuildGraphScriptName"), JsonRequired]
        public string BuildGraphScriptName { get; set; } = string.Empty;

        [JsonPropertyName("PreparationScripts"), JsonRequired]
        public string[] PreparationScripts { get; set; } = new string[0];

        [JsonPropertyName("Settings"), JsonRequired]
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();
    }
}