namespace Redpoint.UET.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginBuild
    {
        /// <summary>
        /// The build configuration for the editor target.
        /// </summary>
        [JsonPropertyName("Editor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginBuildEditor? Editor { get; set; }

        /// <summary>
        /// The build configuration for the game target.
        /// </summary>
        [JsonPropertyName("Game"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginBuildTarget? Game { get; set; }

        /// <summary>
        /// The build configuration for the client target.
        /// </summary>
        [JsonPropertyName("Client"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginBuildTarget? Client { get; set; }

        /// <summary>
        /// The build configuration for the server target.
        /// </summary>
        [JsonPropertyName("Server"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginBuildTarget? Server { get; set; }
    }
}
