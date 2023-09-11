namespace Redpoint.Uet.Configuration.Engine
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigEngineDistribution
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Source")]
        public BuildConfigEngineSource Source { get; set; } = new BuildConfigEngineSource();

        [JsonPropertyName("Build")]
        public BuildConfigEngineBuild Build { get; set; } = new BuildConfigEngineBuild();

        [JsonPropertyName("Cook")]
        public BuildConfigEngineCook Cook { get; set; } = new BuildConfigEngineCook();

        [JsonPropertyName("Deployment")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigEngineDeployment[] Deployment { get; set; } = Array.Empty<BuildConfigEngineDeployment>();

        [JsonPropertyName("MobileProvisions")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigMobileProvision[] MobileProvisions { get; set; } = Array.Empty<BuildConfigMobileProvision>();
    }
}
