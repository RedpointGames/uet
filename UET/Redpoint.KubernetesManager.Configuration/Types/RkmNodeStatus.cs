namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
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
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Used for JSON serialization.")]
        public List<RkmNodePlatform>? CapablePlatforms { get; set; }

        [JsonPropertyName("architecture")]
        public string? Architecture { get; set; }

        [JsonPropertyName("provisioner")]
        public RkmNodeStatusProvisioner? Provisioner { get; set; }

        [JsonPropertyName("lastSuccessfulProvision")]
        public RkmNodeStatusLastSuccessfulProvision? LastSuccessfulProvision { get; set; }

        [JsonPropertyName("registeredIpAddresses")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "We need RemoveAll on this property.")]
        public List<RkmNodeStatusRegisteredIpAddress>? RegisteredIpAddresses { get; set; }

        [JsonPropertyName("bootToDisk")]
        public bool? BootToDisk { get; set; }

        [JsonPropertyName("bootEntries")]
        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "We need RemoveAll on this property.")]
        public List<RkmNodeStatusBootEntry>? BootEntries { get; set; }
    }
}
