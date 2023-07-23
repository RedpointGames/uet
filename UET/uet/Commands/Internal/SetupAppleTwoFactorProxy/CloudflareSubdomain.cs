namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class CloudflareSubdomain
    {
        [JsonPropertyName("subdomain")]
        public string? Subdomain { get; set; }
    }
}
