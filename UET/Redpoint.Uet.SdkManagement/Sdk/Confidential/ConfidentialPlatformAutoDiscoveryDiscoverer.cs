namespace Redpoint.Uet.SdkManagement.Sdk.Confidential
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformAutoDiscoveryDiscoverer
    {
        [JsonPropertyName("PlatformName"), JsonRequired]
        public string PlatformName { get; set; } = string.Empty;

        [JsonPropertyName("RecognisedPlatformNamesForInstall"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[] RecognisedPlatformNamesForInstall { get; set; } = [];

        [JsonPropertyName("EnginePlatformConfigJsonPath"), JsonRequired]
        public string EnginePlatformConfigJsonPath { get; set; } = string.Empty;

        [JsonPropertyName("Config"), JsonRequired]
        public ConfidentialPlatformConfig Config { get; set; } = new();
    }
}
