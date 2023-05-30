namespace Redpoint.UET.Configuration.Engine
{
    using System.Text.Json.Serialization;

    public class BuildConfigEngineIncludeFragment
    {
        [JsonPropertyName("Distributions")]
        public List<BuildConfigEngineDistribution> Distributions { get; set; } =
            new List<BuildConfigEngineDistribution>();
    }
}
