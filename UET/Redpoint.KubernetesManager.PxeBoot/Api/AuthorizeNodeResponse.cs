namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using System.Text.Json.Serialization;

    internal class AuthorizeNodeResponse
    {
        [JsonPropertyName("nodeName")]
        public required string NodeName { get; set; }
    }
}
