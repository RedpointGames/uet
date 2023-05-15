namespace Redpoint.UET.Configuration.Engine
{
    using Redpoint.UET.Configuration;
    using System.Text.Json.Serialization;

    public class BuildConfigEngine : BuildConfig
    {
        [JsonPropertyName("Distributions")]
        public BuildConfigEngineDistribution[] Distributions { get; set; } =
            new BuildConfigEngineDistribution[0];
    }
}
