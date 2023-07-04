namespace Redpoint.Uet.Configuration.Engine
{
    using Redpoint.Uet.Configuration;
    using System.Text.Json.Serialization;

    public class BuildConfigEngine : BuildConfig
    {
        /// <summary>
        /// A list of distributions.
        /// </summary>
        [JsonPropertyName("Distributions")]
        public List<BuildConfigEngineDistribution> Distributions { get; set; } =
            new List<BuildConfigEngineDistribution>();
    }
}
