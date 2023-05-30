namespace Redpoint.UET.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginTestAutomation
    {
        /// <summary>
        /// Only tests with this prefix will be executed for automation testing.
        /// </summary>
        [JsonPropertyName("TestPrefix"), JsonRequired]
        public string TestPrefix { get; set; } = string.Empty;

        /// <summary>
        /// A list of platforms to execute automation tests on. A set of "Win64" and "Mac" (either or both).
        /// </summary>
        [JsonPropertyName("Platforms"), JsonRequired]
        public string[] Platforms { get; set; } = new string[0];

        /// <summary>
        /// A list of folders whose contents should be copied into the Config folder of the host project used for automation testing. This is used to set configuration if your automation tests require specific configuration values to be set.
        /// </summary>
        [JsonPropertyName("ConfigFiles"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? ConfigFiles { get; set; }

        /// <summary>
        /// The minimum number of processes to launch for automation testing on the machine, regardless of available RAM. If not set, defaults to 4.
        /// </summary>
        [JsonPropertyName("MinWorkerCount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MinWorkerCount { get; set; }

        /// <summary>
        /// The timeout in minutes for running the automation test suite. If not set, defaults to 5 minutes.
        /// </summary>
        [JsonPropertyName("TimeoutMinutes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TimeoutMinutes { get; set; }
    }
}
