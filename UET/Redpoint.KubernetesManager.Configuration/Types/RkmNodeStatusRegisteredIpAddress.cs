namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System;
    using System.Text.Json.Serialization;

    public class RkmNodeStatusRegisteredIpAddress
    {
        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
