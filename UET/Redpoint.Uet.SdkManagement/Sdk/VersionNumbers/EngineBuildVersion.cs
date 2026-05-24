namespace Redpoint.Uet.SdkManagement.Sdk.VersionNumbers
{
    using System.Text.Json.Serialization;

    internal class EngineBuildVersion
    {
        [JsonPropertyName("MajorVersion")]
        public int MajorVersion { get; set; }

        [JsonPropertyName("MinorVersion")]
        public int MinorVersion { get; set; }
    }
}
