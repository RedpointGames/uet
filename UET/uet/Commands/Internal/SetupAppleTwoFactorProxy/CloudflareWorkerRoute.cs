namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class CloudflareWorkerRoute
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; }

        [JsonPropertyName("request_limit_fail_open")]
        public bool RequestLimitFailOpen { get; set; }

        [JsonPropertyName("script")]
        public string? Script { get; set; }
    }
}
