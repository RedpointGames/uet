namespace Redpoint.Uet.SdkManagement
{
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformRequiredWindowsSdk
    {
        [JsonPropertyName("WindowsSdkPreferredVersion"), JsonRequired]
        public string? WindowsSdkPreferredVersion { get; set; }

        [JsonPropertyName("VisualCppMinimumVersion"), JsonRequired]
        public string? VisualCppMinimumVersion { get; set; }

        [JsonPropertyName("SuggestedComponents")]
        public string[]? SuggestedComponents { get; set; }

        [JsonPropertyName("SubdirectoryName")]
        public string? SubdirectoryName { get; set; }
    }
}
