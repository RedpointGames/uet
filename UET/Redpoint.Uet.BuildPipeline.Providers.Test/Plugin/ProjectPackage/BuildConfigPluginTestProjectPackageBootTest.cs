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
        /// If set, overrides the CI/CD machine that runs the test node.
        /// </summary>
        [JsonPropertyName("BuildMachineTag")]
        public string? BuildMachineTag { get; set; }

        /// <summary>
        /// If set, the device to deploy to.
        /// </summary>
        [JsonPropertyName("DeviceId")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// Command line arguments to pass to Gauntlet.
        /// </summary>
        [JsonPropertyName("GauntletArguments")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? GauntletArguments { get; set; } = Array.Empty<string>();
    }
}
