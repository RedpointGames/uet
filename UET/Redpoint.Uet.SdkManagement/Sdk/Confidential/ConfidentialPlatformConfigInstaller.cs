namespace Redpoint.Uet.SdkManagement
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformConfigInstaller
    {
        [JsonPropertyName("InstallerPath"), JsonRequired]
        public string? InstallerPath { get; set; }

        [JsonPropertyName("InstallerArguments"), JsonRequired]
        public string[]? InstallerArguments { get; set; }

        [JsonPropertyName("InstallerAdditionalLogFileDirectories")]
        public string[]? InstallerAdditionalLogFileDirectories { get; set; }

        [JsonPropertyName("MustExistAfterInstall")]
        public string[]? MustExistAfterInstall { get; set; }

        [JsonPropertyName("BeforeInstallSetRegistryValue")]
        public Dictionary<string, Dictionary<string, JsonElement>>? BeforeInstallSetRegistryValue { get; set; }

        [JsonPropertyName("AfterInstallSetRegistryValue")]
        public Dictionary<string, Dictionary<string, JsonElement>>? AfterInstallSetRegistryValue { get; set; }

        [JsonPropertyName("PermitNonZeroExitCode")]
        public bool PermitNonZeroExitCode { get; set; }
    }
}
