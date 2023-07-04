namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// test
    /// </summary>
    public class BuildConfigPlugin : BuildConfig
    {
        /// <summary>
        /// The name of your plugin. It must be such that "PluginName/PluginName.uplugin" exists.
        /// </summary>
        [JsonPropertyName("PluginName"), JsonRequired]
        public string PluginName { get; set; } = string.Empty;

        /// <summary>
        /// Used for Marketplace submissions and update-copyright command.
        /// </summary>
        [JsonPropertyName("Copyright"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginCopyright? Copyright { get; set; }

        /// <summary>
        /// A list of distributions.
        /// </summary>
        [JsonPropertyName("Distributions")]
        public List<BuildConfigPluginDistribution> Distributions { get; set; } = new List<BuildConfigPluginDistribution>();
    }
}
