namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginCopyright
    {
        /// <summary>
        /// Used for Marketplace submissions and update-copyright command.
        /// </summary>
        [JsonPropertyName("Header"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Header { get; set; }

        /// <summary>
        /// If set, these files will not have their headers updated during packaging.
        /// </summary>
        [JsonPropertyName("ExcludePaths"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? ExcludePaths { get; set; }
    }
}
