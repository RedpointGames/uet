namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.GooglePlay
{
    using System.Text.Json.Serialization;

    internal class PartnerCenterConfigFileRemovePackages
    {
        [JsonPropertyName("operationName"), JsonRequired]
        public required string OperationName { get; set; }

        [JsonPropertyName("aadAuthInfo"), JsonRequired]
        public required PartnerCenterAadAuthInfo AadAuthInfo { get; set; }

        [JsonPropertyName("bigId"), JsonRequired]
        public required string BigId { get; set; }

        [JsonPropertyName("branchFriendlyName"), JsonRequired]
        public required string BranchFriendlyName { get; set; }

        [JsonPropertyName("packageFileName"), JsonRequired]
        public required string PackageFileName { get; set; }
    }
}
