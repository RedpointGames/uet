namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.GooglePlay
{
    using System.Text.Json.Serialization;

    internal class PartnerCenterAadAuthInfo
    {
        [JsonPropertyName("tenantId"), JsonRequired]
        public required string TenantId { get; set; }

        [JsonPropertyName("clientId"), JsonRequired]
        public required string ClientId { get; set; }
    }
}
