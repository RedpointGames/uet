namespace Redpoint.UET.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginPackage
    {
        /// <summary>
        /// If true, the plugin is packaged for Marketplace submission. If not set, defaults to false.
        /// </summary>
        [JsonPropertyName("Marketplace"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Marketplace { get; set; }

        /// <summary>
        /// If not set, defaults to "Packaged".
        /// </summary>
        [JsonPropertyName("OutputFolderName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? OutputFolderName { get; set; }

        /// <summary>
        /// The path to the FilterPlugin.ini file used for packaging.
        /// </summary>
        [JsonPropertyName("Filter"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Filter { get; set; }
    }
}
