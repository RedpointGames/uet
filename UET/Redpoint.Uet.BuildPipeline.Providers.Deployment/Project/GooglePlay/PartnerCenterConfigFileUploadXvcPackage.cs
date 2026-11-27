namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.GooglePlay
{
    using System.Text.Json.Serialization;

    internal class PartnerCenterConfigFileUploadXvcPackage
    {
        [JsonPropertyName("operationName"), JsonRequired]
        public required string OperationName { get; set; }

        [JsonPropertyName("aadAuthInfo"), JsonRequired]
        public required PartnerCenterAadAuthInfo AadAuthInfo { get; set; }

        [JsonPropertyName("bigId"), JsonRequired]
        public required string BigId { get; set; }

        [JsonPropertyName("branchFriendlyName"), JsonRequired]
        public required string BranchFriendlyName { get; set; }

        [JsonPropertyName("packageFilePath"), JsonRequired]
        public required string PackageFilePath { get; set; }

        [JsonPropertyName("deltaUpload"), JsonRequired]
        public required bool DeltaUpload { get; set; }

        [JsonPropertyName("gameAssets"), JsonRequired]
        public required PartnerCenterGameAssets GameAssets { get; set; }
    }
}
