namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginIncludeFragment
    {
        /// <summary>
        /// A list of distributions.
        /// </summary>
        [JsonPropertyName("Distributions"), JsonRequired]
        public BuildConfigPluginDistribution[] Distributions { get; set; } = Array.Empty<BuildConfigPluginDistribution>();
    }
}
