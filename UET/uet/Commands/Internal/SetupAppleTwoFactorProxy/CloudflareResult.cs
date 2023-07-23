namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class CloudflareResult<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("errors")]
        public CloudflareMessage[]? Errors { get; set; }

        [JsonPropertyName("messages")]
        public CloudflareMessage[]? Messages { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }
    }
}
