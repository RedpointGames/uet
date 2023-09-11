namespace Redpoint.Uet.SdkManagement
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformAutoSdkSetupScript
    {
        [JsonPropertyName("TargetPath"), JsonRequired]
        public string? TargetPath { get; set; }

        [JsonPropertyName("Lines"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? Lines { get; set; }
    }
}
