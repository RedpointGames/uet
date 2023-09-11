namespace Redpoint.Uet.Configuration.Engine
{
    using Redpoint.Uet.Configuration;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigEngine : BuildConfig
    {
        /// <summary>
        /// A list of distributions.
        /// </summary>
        [JsonPropertyName("Distributions")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "This property is used for JSON serialization.")]
        public List<BuildConfigEngineDistribution> Distributions { get; set; } =
            new List<BuildConfigEngineDistribution>();
    }
}
