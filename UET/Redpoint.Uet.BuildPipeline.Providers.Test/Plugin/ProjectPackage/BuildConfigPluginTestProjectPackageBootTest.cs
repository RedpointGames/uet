namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.ProjectPackage
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Configuration for booting a packaged project on device.
    /// </summary>
    public class BuildConfigPluginTestProjectPackageBootTest
    {
        /// <summary>
        /// The automation tests to run as part of the boot test.
        /// </summary>
        [JsonPropertyName("AutomationTests"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? AutomationTests { get; set; }

        /// <summary>
        /// If set, overrides the CI/CD machine that runs the test node.
        /// </summary>
        [JsonPropertyName("BuildMachineTag")]
        public string? BuildMachineTag { get; set; }

        /// <summary>
        /// If set, the device to deploy to.
        /// </summary>
        [JsonPropertyName("DeviceId")]
        public string? DeviceId { get; set; }
    }
}
