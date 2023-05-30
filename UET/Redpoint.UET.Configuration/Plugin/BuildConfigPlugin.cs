namespace Redpoint.UET.Configuration.Plugin
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class BuildConfigPlugin : BuildConfig
    {
        /// <summary>
        /// The name of your plugin. It must be such that "PluginName/PluginName.uplugin" exists.
        /// </summary>
        [JsonPropertyName("PluginName"), JsonRequired]
        public string PluginName { get; set; } = string.Empty;

        /// <summary>
        /// Used for the update-copyright command.
        /// </summary>
        [JsonPropertyName("CopyrightHeader"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CopyrightHeader { get; set; }

        /// <summary>
        /// A list of distributions.
        /// </summary>
        [JsonPropertyName("Distributions"), JsonRequired]
        public BuildConfigPluginDistribution[] Distributions { get; set; } = new BuildConfigPluginDistribution[0];
    }
}
