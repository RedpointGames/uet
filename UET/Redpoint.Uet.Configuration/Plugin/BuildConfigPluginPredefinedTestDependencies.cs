namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Specifies how to how to package and build the plugin so this test can be run.
    /// </summary>
    public class BuildConfigPluginPredefinedTestDependencies
    {
        /// <summary>
        /// A list of environment variables to set during the build.
        /// </summary>
        [JsonPropertyName("EnvironmentVariables"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        /// <summary>
        /// Specifies how to build the plugin to test it.
        /// </summary>
        [JsonPropertyName("Build"), JsonRequired]
        public BuildConfigPluginBuild? Build { get; set; }

        /// <summary>
        /// Specifies how to package the plugin in order to test it.
        /// </summary>
        [JsonPropertyName("Package"), JsonRequired]
        public BuildConfigPluginPackage? Package { get; set; }
    }
}
