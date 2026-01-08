namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using System.Text.Json.Serialization;

    internal class AuthorizeNodeResponse
    {
        [JsonPropertyName("nodeName")]
        public required string NodeName { get; set; }

        [JsonPropertyName("aikFingerprint")]
        public required string AikFingerprint { get; set; }

        [JsonPropertyName("parameterValues")]
        public required Dictionary<string, string> ParameterValues { get; set; }
    }
}
