namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class PlivoApplicationCreateRequest
    {
        [JsonPropertyName("app_name"), JsonRequired]
        public string? AppName { get; set; }

        [JsonPropertyName("message_url"), JsonRequired]
        public string? MessageUrl { get; set; }
    }
}
