namespace Redpoint.UET.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginClean
    {
        /// <summary>
        /// A list of filespecs that should be cleaned. Use * as a wildcard for a single path component, or ... as a wildcard for multiple path components.
        /// </summary>
        [JsonPropertyName("Filespecs"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Filespecs { get; set; }
    }
}
