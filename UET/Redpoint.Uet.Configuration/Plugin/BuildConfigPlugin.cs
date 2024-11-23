namespace Redpoint.Uet.Configuration.Plugin
{
    using Redpoint.Uet.Configuration.Dynamic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigPlugin : BuildConfig
    {
        /// <summary>
        /// The name of your plugin. It must be such that "PluginName/PluginName.uplugin" exists.
        /// </summary>
        [JsonPropertyName("PluginName"), JsonRequired]
        public string PluginName { get; set; } = string.Empty;

        /// <summary>
        /// Used for Marketplace/Fab submissions and update-copyright command.
        /// </summary>
        [JsonPropertyName("Copyright"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginCopyright? Copyright { get; set; }

        /// <summary>
        /// A list of predefined tests that you can use with `uet test` or in other "Tests" sections for distributions.
        /// </summary>
        [JsonPropertyName("Tests"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigPredefinedDynamic<BuildConfigPluginDistribution, ITestProvider, BuildConfigPluginPredefinedTestDependencies>[]? Tests { get; set; }

        /// <summary>
        /// A list of distributions.
        /// </summary>
        [JsonPropertyName("Distributions")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "This property is used for JSON serialization.")]
        public List<BuildConfigPluginDistribution> Distributions { get; set; } = new List<BuildConfigPluginDistribution>();
    }
}
