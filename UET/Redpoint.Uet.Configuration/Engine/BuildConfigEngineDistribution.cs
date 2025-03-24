namespace Redpoint.Uet.Configuration.Engine
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigEngineDistribution
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// If set, specifies that the engine source code should come from another repository. If not set, 
        /// it is expected that the BuildConfig.json file is in the root of the engine repository, such that 
        /// it is in the same folder that contains the "Engine" folder.
        /// </summary>
        [JsonPropertyName("ExternalSource")]
        public BuildConfigEngineExternalSource? ExternalSource { get; set; }

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
