namespace Docker.Registry.DotNet.OAuth
{
    using System;
    using System.Text.Json.Serialization;

    internal class OAuthToken
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("issued_at")]
        public DateTime IssuedAt { get; set; }
    }
}