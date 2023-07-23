namespace UET.Commands.Internal.SetupAppleTwoFactorProxy
{
    using System.Text.Json.Serialization;

    internal class PlivoSubaccount
    {
        [JsonPropertyName("account"), JsonRequired]
        public string? Account { get; set; }

        [JsonPropertyName("auth_id"), JsonRequired]
        public string? AuthId { get; set; }

        [JsonPropertyName("auth_token"), JsonRequired]
        public string? AuthToken { get; set; }

        [JsonPropertyName("created"), JsonRequired]
        public string? Created { get; set; }

        [JsonPropertyName("enabled"), JsonRequired]
        public bool Enabled { get; set; }

        [JsonPropertyName("modified"), JsonRequired]
        public string? Modified { get; set; }

        [JsonPropertyName("name"), JsonRequired]
        public string? Name { get; set; }

        [JsonPropertyName("resource_uri"), JsonRequired]
        public string? ResourceUri { get; set; }
    }
}
