namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class RkmNodeStatus
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
        public IList<RkmNodePlatform>? CapablePlatforms { get; set; }

        [JsonPropertyName("architecture")]
        public string? Architecture { get; set; }

        [JsonPropertyName("provisioner")]
        public RkmNodeStatusProvisioner? Provisioner { get; set; }
    }
}
