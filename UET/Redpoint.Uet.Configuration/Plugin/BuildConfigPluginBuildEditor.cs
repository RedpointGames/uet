namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginBuildEditor
    {
        /// <summary>
        /// A list of platforms to build the plugin for on the editor target.
        /// </summary>
        [JsonPropertyName("Platforms"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginBuildEditorPlatform[]? Platforms { get; set; }
    }
}
