namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class CloudflareSubdomainEnable
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}
