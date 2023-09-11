namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigPluginClean
    {
        /// <summary>
        /// A list of filespecs that should be cleaned. Use * as a wildcard for a single path component, or ... as a wildcard for multiple path components.
        /// </summary>
        [JsonPropertyName("Filespecs"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? Filespecs { get; set; }
    }
}
