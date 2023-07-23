namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class CloudflareMessage
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
