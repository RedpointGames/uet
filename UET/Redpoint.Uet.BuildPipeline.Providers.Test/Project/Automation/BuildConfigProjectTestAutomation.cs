namespace Redpoint.Uet.BuildPipeline.Providers.Test.Project.Automation
{
    using Redpoint.Uet.Configuration;
    using System.Text.Json.Serialization;

    public class BuildConfigProjectTestAutomation
    {
        /// <summary>
        /// Only tests with this prefix will be executed for automation testing.
        /// </summary>
        [JsonPropertyName("TestPrefix"), JsonRequired]
        public string TestPrefix { get; set; } = string.Empty;

        /// <summary>
        /// The name of the editor target to run the tests against. If not set, defaults to UnrealEditor.
        /// </summary>
        [JsonPropertyName("TargetName")]
        public string? TargetName { get; set; } = null;

        /// <summary>
        /// The minimum number of processes to launch for automation testing on the machine, regardless of available RAM. If not set, defaults to 1.
        /// </summary>
        [JsonPropertyName("MinWorkerCount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MinWorkerCount { get; set; }

        /// <summary>
        /// The timeout in minutes for running the whole automation test suite. If not set, defaults to 5 minutes.
        /// </summary>
        [JsonPropertyName("TestRunTimeoutMinutes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TestRunTimeoutMinutes { get; set; }

        /// <summary>
        /// The timeout in minutes for running an individual test. If not set, no timeout applies.
        /// </summary>
        [JsonPropertyName("TestTimeoutMinutes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TestTimeoutMinutes { get; set; }

        /// <summary>
        /// The maximum number of times a test should be attempted if it fails. If not set, tests are never retried on failure.
        /// </summary>
        [JsonPropertyName("TestAttemptCount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TestAttemptCount { get; set; }
    }
}
