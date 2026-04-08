namespace Redpoint.Uet.SdkManagement.Sdk.Confidential
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformAutoDiscovery
    {
        [JsonPropertyName("FileStoragePath"), JsonRequired]
        public string FileStoragePath { get; set; } = string.Empty;

        [JsonPropertyName("Discoverers"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public ConfidentialPlatformAutoDiscoveryDiscoverer[] Discoverers { get; set; } = [];
    }
}
