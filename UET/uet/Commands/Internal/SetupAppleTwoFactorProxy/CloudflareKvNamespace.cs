namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class CloudflareKvNamespace
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("supports_url_encoding")]
        public bool SupportsUrlEncoding { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }
}
