namespace Redpoint.Uet.BuildPipeline.Providers.Test.Project.Gauntlet
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigProjectTestGauntletRequire
    {
        /// <summary>
        /// Specifies the dependency type (Game, Client or Server).
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigProjectTestGauntletRequireType Type { get; set; } = BuildConfigProjectTestGauntletRequireType.Game;

        /// <summary>
        /// The target to depend on.
        /// </summary>
        [JsonPropertyName("Target"), JsonRequired]
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// The platforms to depend on.
        /// </summary>
        [JsonPropertyName("Platforms"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? Platforms { get; set; } = null;

        /// <summary>
        /// The configuration to depend on.
        /// </summary>
        [JsonPropertyName("Configuration"), JsonRequired]
        public string Configuration { get; set; } = string.Empty;
    }
}
