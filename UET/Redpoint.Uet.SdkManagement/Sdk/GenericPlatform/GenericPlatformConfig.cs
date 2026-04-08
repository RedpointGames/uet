namespace Redpoint.Uet.SdkManagement.Sdk.GenericPlatform
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;

    public class GenericPlatformConfig
    {
        [JsonPropertyName("MainVersion")]
        public string? MainVersion { get; set; }

        [JsonPropertyName("MinVersion")]
        public string? MinVersion { get; set; }

        [JsonPropertyName("MaxVersion")]
        public string? MaxVersion { get; set; }
    }
}
