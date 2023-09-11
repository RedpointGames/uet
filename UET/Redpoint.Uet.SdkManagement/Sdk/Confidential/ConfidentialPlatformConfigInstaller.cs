namespace Redpoint.Uet.SdkManagement
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformConfigInstaller
    {
        [JsonPropertyName("InstallerPath"), JsonRequired]
        public string? InstallerPath { get; set; }

        [JsonPropertyName("InstallerArguments"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? InstallerArguments { get; set; }

        [JsonPropertyName("InstallerAdditionalLogFileDirectories")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? InstallerAdditionalLogFileDirectories { get; set; }

        [JsonPropertyName("MustExistAfterInstall")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? MustExistAfterInstall { get; set; }

        [JsonPropertyName("BeforeInstallSetRegistryValue")]
        public Dictionary<string, Dictionary<string, JsonElement>>? BeforeInstallSetRegistryValue { get; set; }

        [JsonPropertyName("AfterInstallSetRegistryValue")]
        public Dictionary<string, Dictionary<string, JsonElement>>? AfterInstallSetRegistryValue { get; set; }

        [JsonPropertyName("PermitNonZeroExitCode")]
        public bool PermitNonZeroExitCode { get; set; }
    }
}
