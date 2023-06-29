namespace Redpoint.Uet.Configuration.Engine
{
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
        public BuildConfigEngineDeployment[] Deployment { get; set; } = new BuildConfigEngineDeployment[0];
    }
}
