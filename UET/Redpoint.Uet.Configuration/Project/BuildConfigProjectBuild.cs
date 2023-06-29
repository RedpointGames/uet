namespace Redpoint.Uet.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectBuild
    {
        /// <summary>
        /// The build configuration for the editor target.
        /// </summary>
        [JsonPropertyName("Editor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectBuildEditor? Editor { get; set; }

        /// <summary>
        /// The build configuration for the game target.
        /// </summary>
        [JsonPropertyName("Game"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectBuildTarget? Game { get; set; }

        /// <summary>
        /// The build configuration for the client target.
        /// </summary>
        [JsonPropertyName("Client"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectBuildTarget? Client { get; set; }

        /// <summary>
        /// The build configuration for the server target.
        /// </summary>
        [JsonPropertyName("Server"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectBuildTarget? Server { get; set; }
    }
}
