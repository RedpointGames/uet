namespace Redpoint.Uet.SdkManagement.Sdk.GenericPlatform
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;

    public class GenericPlatformConfig
    {
        [JsonPropertyName("IncludeSDKFile")]
        public string? IncludeSDKFile { get; set; }

        [JsonPropertyName("MainVersion")]
        public string? MainVersion { get; set; }

        [JsonPropertyName("MinVersion")]
        public string? MinVersion { get; set; }

        [JsonPropertyName("MaxVersion")]
        public string? MaxVersion { get; set; }

        [JsonPropertyName("MainGDKVersion")]
        public string? MainGDKVersion { get; set; }

        [JsonPropertyName("MinGDKVersion")]
        public string? MinGDKVersion { get; set; }

        [JsonPropertyName("MaxGDKVersion")]
        public string? MaxGDKVersion { get; set; }
    }
}
