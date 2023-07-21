namespace Redpoint.Uet.SdkManagement
{
    using System.Text.Json.Serialization;

    public class ConfidentialPlatformAutoSdkSetupScript
    {
        [JsonPropertyName("TargetPath"), JsonRequired]
        public string? TargetPath { get; set; }

        [JsonPropertyName("Lines"), JsonRequired]
        public string[]? Lines { get; set; }
    }
}
