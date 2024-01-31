namespace Redpoint.Uet.BuildPipeline.Executors.BuildServer
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildJobJson
    {
        [JsonPropertyName("Engine"), JsonRequired]
        public string Engine { get; set; } = string.Empty;

        /// <summary>
        /// If true, the engine itself is being built, rather than building a project or plugin with the engine.
        /// </summary>
        [JsonPropertyName("IsEngineBuild"), JsonRequired]
        public bool IsEngineBuild { get; set; } = false;

        [JsonPropertyName("SharedStoragePath"), JsonRequired]
        public string SharedStoragePath { get; set; } = string.Empty;

        [JsonPropertyName("SdksPath"), JsonRequired]
        public string? SdksPath { get; set; } = null;

        [JsonPropertyName("BuildGraphTarget"), JsonRequired]
        public string BuildGraphTarget { get; set; } = string.Empty;

        [JsonPropertyName("NodeNames"), JsonRequired]
        public IReadOnlyList<string> NodeNames { get; set; } = Array.Empty<string>();

        [JsonPropertyName("DistributionName"), JsonRequired]
        public string DistributionName { get; set; } = string.Empty;

        [JsonPropertyName("BuildGraphScriptName"), JsonRequired]
        public string BuildGraphScriptName { get; set; } = string.Empty;

        [JsonPropertyName("PreparePlugin"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>[]? PreparePlugin { get; set; }

        [JsonPropertyName("PrepareProject"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>[]? PrepareProject { get; set; }

        [JsonPropertyName("GlobalEnvironmentVariables"), JsonRequired]
        public Dictionary<string, string> GlobalEnvironmentVariables { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("Settings"), JsonRequired]
        public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("ProjectFolderName"), JsonRequired]
        public string? ProjectFolderName { get; set; } = null;

        [JsonPropertyName("UseStorageVirtualisation"), JsonRequired]
        public bool UseStorageVirtualisation { get; set; } = false;

        [JsonPropertyName("MobileProvisions"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigMobileProvision[] MobileProvisions { get; set; } = Array.Empty<BuildConfigMobileProvision>();
    }
}