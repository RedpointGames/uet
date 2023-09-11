namespace Redpoint.Uet.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectBuildTarget
    {
        /// <summary>
        /// A list of targets to build.
        /// </summary>
        [JsonPropertyName("Targets"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Targets { get; set; } = null;

        /// <summary>
        /// A list of platforms to build the project for.
        /// </summary>
        [JsonPropertyName("Platforms")]
        public string[] Platforms { get; set; } = Array.Empty<string>();

        /// <summary>
        /// If not specified, defaults to ["Development", "Shipping"].
        /// </summary>
        [JsonPropertyName("Configurations"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Configurations { get; set; } = null;
    }
}
