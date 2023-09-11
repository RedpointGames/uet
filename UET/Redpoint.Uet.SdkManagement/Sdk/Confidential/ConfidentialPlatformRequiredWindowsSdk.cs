namespace Redpoint.Uet.SdkManagement
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformRequiredWindowsSdk
    {
        [JsonPropertyName("WindowsSdkPreferredVersion"), JsonRequired]
        public string? WindowsSdkPreferredVersion { get; set; }

        [JsonPropertyName("VisualCppMinimumVersion"), JsonRequired]
        public string? VisualCppMinimumVersion { get; set; }

        [JsonPropertyName("SuggestedComponents")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? SuggestedComponents { get; set; }

        [JsonPropertyName("SubdirectoryName")]
        public string? SubdirectoryName { get; set; }
    }
}
