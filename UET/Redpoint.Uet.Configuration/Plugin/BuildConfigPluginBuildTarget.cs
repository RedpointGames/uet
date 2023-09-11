namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigPluginBuildTarget
    {
        /// <summary>
        /// A list of platforms to build the plugin for on the target.
        /// </summary>
        [JsonPropertyName("Platforms")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[] Platforms { get; set; } = Array.Empty<string>();

        /// <summary>
        /// If not specified, defaults to ["Development", "Shipping"].
        /// </summary>
        [JsonPropertyName("Configurations")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[] Configurations { get; set; } = new string[] { "Development", "Shipping" };
    }
}
