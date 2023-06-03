namespace Redpoint.UET.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginTestGauntlet
    {
        /// <summary>
        /// Specifies the configurations and platforms that the Gauntlet tests is dependent on.
        /// </summary>
        [JsonPropertyName("Requires")]
        public BuildConfigPluginTestGauntletRequire[]? Requires { get; set; }

        /// <summary>
        /// The relative path to the Gauntlet script to execute.
        /// </summary>
        [JsonPropertyName("ScriptPath"), JsonRequired]
        public string ScriptPath { get; set; } = string.Empty;

        /// <summary>
        /// The relative path to the device list to use.
        /// </summary>
        [JsonPropertyName("DeviceListPath"), JsonRequired]
        public string DeviceListPath { get; set; } = string.Empty;

        /// <summary>
        /// The name of the test to run inside the Gauntlet script.
        /// </summary>
        [JsonPropertyName("TestName"), JsonRequired]
        public string TestName { get; set; } = string.Empty;
    }
}
