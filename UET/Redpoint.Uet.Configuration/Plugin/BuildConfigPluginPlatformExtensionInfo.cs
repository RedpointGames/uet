namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigPluginPlatformExtensionInfo
    {
        /// <summary>
        /// When running on CI/CD, the Git URL of the repository to clone.
        /// </summary>
        [JsonPropertyName("UpstreamUrl"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "This property is used for JSON serialization.")]
        public string? UpstreamUrl { get; set; }

        /// <summary>
        /// When running on CI/CD, the Git ref of the repository to clone.
        /// </summary>
        [JsonPropertyName("UpstreamRef"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UpstreamRef { get; set; }

        /// <summary>
        /// When not running on CI/CD, the local relative path to the base code.
        /// </summary>
        [JsonPropertyName("UpstreamLocalPath"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? UpstreamLocalPath { get; set; }

        /// <summary>
        /// The platform this extension is for.
        /// </summary>
        [JsonPropertyName("Platform"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Platform { get; set; }
    }
}
