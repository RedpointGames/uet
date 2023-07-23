namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class CloudflareKvNamespaceCreate
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }
}
