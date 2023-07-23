namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class PlivoNumber
    {
        [JsonPropertyName("number"), JsonRequired]
        public string? Number { get; set; }

        [JsonPropertyName("alias"), JsonRequired]
        public string? Alias { get; set; }
    }
}
