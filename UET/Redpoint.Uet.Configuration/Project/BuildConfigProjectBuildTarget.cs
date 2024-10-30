namespace Redpoint.Uet.Configuration.Project
{
    using Redpoint.Uet.Configuration;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigProjectBuildTarget
    {
        /// <summary>
        /// A list of targets to build.
        /// </summary>
        [JsonPropertyName("Targets"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? Targets { get; set; } = null;

        /// <summary>
        /// A list of platforms to build the project for.
        /// </summary>
        [JsonPropertyName("Platforms")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigTargetPlatform[] Platforms { get; set; } = [];

        /// <summary>
        /// If not specified, defaults to ["Development", "Shipping"].
        /// </summary>
        [JsonPropertyName("Configurations"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? Configurations { get; set; } = null;
    }
}
