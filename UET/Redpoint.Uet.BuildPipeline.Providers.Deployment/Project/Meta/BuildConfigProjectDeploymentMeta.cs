namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Meta
{
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using System.Text.Json.Serialization;

    public class BuildConfigProjectDeploymentMeta
    {
        /// <summary>
        /// The Meta Quest app ID to deploy to.
        /// </summary>
        [JsonPropertyName("AppID"), JsonRequired]
        public string AppID { get; set; } = string.Empty;

        /// <summary>
        /// The environment variable that contains the app secret.
        /// </summary>
        [JsonPropertyName("AppSecretEnvVar"), JsonRequired]
        public string AppSecretEnvVar { get; set; } = string.Empty;

        /// <summary>
        /// The channel to deploy to (e.g. ALPHA)
        /// </summary>
        [JsonPropertyName("Channel"), JsonRequired]
        public string Channel { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the packaging target to be deployed.
        /// </summary>
        [JsonPropertyName("Package"), JsonRequired]
        public BuildConfigProjectDeploymentPackage Package { get; set; } = new BuildConfigProjectDeploymentPackage();
    }
}
