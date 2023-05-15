namespace BuildRunner.Configuration.Engine
{
    using System.Text.Json.Serialization;

    internal class BuildConfigEngine : BuildConfig
    {
        [JsonPropertyName("Distributions")]
        public BuildConfigEngineDistribution[] Distributions { get; set; } =
            new BuildConfigEngineDistribution[0];
    }
}
