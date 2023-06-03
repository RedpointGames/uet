namespace Redpoint.UET.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginTest
    {
        /// <summary>
        /// The name of the job/step as it would be displayed on a build server. This must be unique amongst all tests defined.
        /// </summary>
        [JsonPropertyName("Name"), JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the test type.
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigPluginTestType Type { get; set; }

        /// <summary>
        /// Specifies the settings for executing automation tests.
        /// </summary>
        [JsonPropertyName("Automation"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginTestAutomation? Automation { get; set; }

        /// <summary>
        /// Specifies the settings for executing Gauntlet tests.
        /// </summary>
        [JsonPropertyName("Gauntlet"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginTestGauntlet? Gauntlet { get; set; }

        /// <summary>
        /// Specifies the settings for executing custom tests.
        /// </summary>
        [JsonPropertyName("Custom"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginTestCustom? Custom { get; set; }
    }
}
