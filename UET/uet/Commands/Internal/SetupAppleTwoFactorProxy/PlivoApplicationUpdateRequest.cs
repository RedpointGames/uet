namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class PlivoApplicationUpdateRequest
    {
        [JsonPropertyName("message_url"), JsonRequired]
        public string? MessageUrl { get; set; }
    }
}
