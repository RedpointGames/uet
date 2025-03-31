namespace Redpoint.Uet.Configuration.Plugin
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

        /// <summary>
        /// Forces --strict-includes for builds.
        /// </summary>
        [JsonPropertyName("StrictIncludes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? StrictIncludes { get; set; }

        /// <summary>
        /// If enabled, debug symbols and PDBs will be omitted, even for non-Shipping configurations.
        /// </summary>
        [JsonPropertyName("StripDebugSymbols"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? StripDebugSymbols { get; set; }

        /// <summary>
        /// If enabled, the plugin will only be built for the Apple ARM64 architecture (and not legacy 
        /// Intel x64) when building for macOS. This can significantly reduce compilation times when you 
        /// don't need Intel support.
        /// </summary>
        [JsonPropertyName("AppleArchitectureOnly"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? AppleArchitectureOnly { get; set; }
    }
}
