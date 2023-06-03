namespace Redpoint.UET.Configuration.Plugin
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class BuildConfigPluginDistribution
    {
        /// <summary>
        /// The name, as passed to the --distribution argument.
        /// </summary>
        [JsonPropertyName("Name"), JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// A list of environment variables to set during the build.
        /// </summary>
        [JsonPropertyName("EnvironmentVariables"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        /// <summary>
        /// Specifies the clean operations to run before building.
        /// </summary>
        [JsonPropertyName("Clean"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginClean? Clean { get; set; }

        /// <summary>
        /// Specifies the preparation scripts to run before various steps. You can specify multiple preparation entries.
        /// </summary>
        [JsonPropertyName("Prepare"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginPrepare[]? Prepare { get; set; }

        /// <summary>
        /// Specifies how the plugin is built.
        /// </summary>
        [JsonPropertyName("Build")]
        public BuildConfigPluginBuild Build { get; set; } = new BuildConfigPluginBuild();

        /// <summary>
        /// A list of tests to run for the plugin.
        /// </summary>
        [JsonPropertyName("Tests"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginTest[]? Tests { get; set; }

        /// <summary>
        /// Specifies the packaging settings.
        /// </summary>
        [JsonPropertyName("Package"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginPackage? Package { get; set; }

        /// <summary>
        /// Specifies the deployment steps.
        /// </summary>
        [JsonPropertyName("Deployment"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginDeployment[]? Deployment { get; set; }

        /// <summary>
        /// Specifies the settings that apply to all Gauntlet tests.
        /// </summary>
        [JsonPropertyName("Gauntlet"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginGauntlet? Gauntlet { get; set; }
    }
}
