namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.PartnerCenter
{
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json.Serialization;

    internal class BuildConfigProjectDeploymentPartnerCenter
    {
        /// <summary>
        /// The product ID to deploy to, displayed under the "Game setup" tab of the app in Partner Center as the "Store ID".
        /// </summary>
        [JsonPropertyName("ProductId"), JsonRequired]
        public required string ProductId { get; set; }

        /// <summary>
        /// The branch display name to deploy to; defaults to "Main".
        /// </summary>
        [JsonPropertyName("BranchDisplayName")]
        public string BranchDisplayName { get; set; } = "Main";

        /// <summary>
        /// The sandbox or 'RETAIL' to automatically publish to. If not set, packages are uploaded as a draft but are not actually published to any sandbox or retail.
        /// </summary>
        [JsonPropertyName("PublishToSandbox")]
        public string? PublishToSandbox { get; set; }

        /// <summary>
        /// Specifies the packaging targets to be deployed.
        /// </summary>
        [JsonPropertyName("Packages"), JsonRequired]
        public BuildConfigProjectDeploymentPackage[] Packages { get; set; } = [];
    }
}
