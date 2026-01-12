namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using System.Text.Json.Serialization;

    internal class CheckNodeAuthorizedResponse
    {
        [JsonPropertyName("authorized")]
        public required bool Authorized { get; set; }
    }
}
