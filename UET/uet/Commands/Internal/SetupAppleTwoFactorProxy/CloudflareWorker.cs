namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class CloudflareWorker
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("handlers")]
        public string[]? Handlers { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("routes")]
        public CloudflareWorkerRoute[]? Routes { get; set; }
    }
}
