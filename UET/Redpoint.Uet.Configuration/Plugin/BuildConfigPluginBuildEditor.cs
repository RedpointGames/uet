namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigPluginBuildEditor
    {
        /// <summary>
        /// A list of platforms to build the plugin for on the editor target.
        /// </summary>
        [JsonPropertyName("Platforms"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigPluginBuildEditorPlatform[]? Platforms { get; set; }
    }
}
