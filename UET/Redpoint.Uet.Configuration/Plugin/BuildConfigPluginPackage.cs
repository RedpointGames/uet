namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginPackage
    {
        /// <summary>
        /// Defines the package type, such as whether it is being packaged for Marketplace or Fab submission. One of 'Generic', 'Marketplace' or 'Fab'. If not set, defaults to 'Generic'.
        /// </summary>
        public BuildConfigPluginPackageType? Type { get; set; }

        /// <summary>
        /// DEPRECATED. Use the 'Type' setting instead.
        /// </summary>
        [JsonPropertyName("Marketplace"), Obsolete("Use the 'Type' attribute instead."), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
