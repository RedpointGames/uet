namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginBuildTarget
    {
        /// <summary>
        /// A list of platforms to build the plugin for on the target.
        /// </summary>
        [JsonPropertyName("Platforms")]
        public string[] Platforms { get; set; } = Array.Empty<string>();

        /// <summary>
        /// If not specified, defaults to ["Development", "Shipping"].
        /// </summary>
        [JsonPropertyName("Configurations")]
        public string[] Configurations { get; set; } = new string[] { "Development", "Shipping" };
    }
}
