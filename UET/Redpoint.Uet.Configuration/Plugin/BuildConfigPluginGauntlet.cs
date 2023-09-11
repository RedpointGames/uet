namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigPluginGauntlet
    {
        /// <summary>
        /// Configuration files to apply to the project that hosts Gauntlet tests.
        /// </summary>
        [JsonPropertyName("ConfigFiles"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? ConfigFiles { get; set; } = null;
    }
}
