namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectDeploymentPackage
    {
        /// <summary>
        /// The type of target to deploy.
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigProjectDeploymentPackageType Type { get; set; } = BuildConfigProjectDeploymentPackageType.Game;

        /// <summary>
        /// The target to deploy.
        /// </summary>
        [JsonPropertyName("Target"), JsonRequired]
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// The platform to deploy.
        /// </summary>
        [JsonPropertyName("Platform"), JsonRequired]
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// The configuration to deploy.
        /// </summary>
        [JsonPropertyName("Configuration"), JsonRequired]
        public string Configuration { get; set; } = string.Empty;
    }
}
