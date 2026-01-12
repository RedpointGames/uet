namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using System.Text.Json.Serialization;

    internal class CheckNodeAuthorizedRequest
    {
        [JsonPropertyName("aikFingerprint")]
        public required string AikFingerprint { get; set; }

        [JsonPropertyName("nodeName")]
        public required string NodeName { get; set; }
    }
}
