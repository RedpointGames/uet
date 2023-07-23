namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class PlivoApplicationCreateResponse
    {
        [JsonPropertyName("app_id"), JsonRequired]
        public string? AppId { get; set; }
    }
}
