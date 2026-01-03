namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class PatchRkmNodeStatusPartialUpdate
    {
        [JsonPropertyName("roles")]
        public IList<RkmNodeRole>? Roles { get; set; }

        [JsonPropertyName("immutable")]
        public bool Immutable { get; set; }

        [JsonPropertyName("attestationIdentityKeyFingerprint")]
        public string? AttestationIdentityKeyFingerprint { get; set; }

        [JsonPropertyName("attestationIdentityKeyPem")]
        public string? AttestationIdentityKeyPem { get; set; }

        [JsonPropertyName("firstSeen")]
        public DateTimeOffset? FirstSeen { get; set; }

        [JsonPropertyName("mostRecentJoinRequest")]
        public DateTimeOffset? MostRecentJoinRequest { get; set; }

        [JsonPropertyName("capablePlatforms")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Used for JSON serialization.")]
        public List<RkmNodePlatform>? CapablePlatforms { get; set; }

        [JsonPropertyName("architecture")]
        public string? Architecture { get; set; }
    }
}
