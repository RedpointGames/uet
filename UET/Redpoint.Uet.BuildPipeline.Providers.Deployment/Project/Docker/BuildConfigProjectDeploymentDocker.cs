namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Docker
{
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using System.Text.Json.Serialization;

    public class BuildConfigProjectDeploymentDocker
    {
        /// <summary>
        /// The Docker image tag without the version component.
        /// </summary>
        [JsonPropertyName("Image"), JsonRequired]
        public string Image { get; set; } = string.Empty;

        /// <summary>
        /// If true, the image is pushed to the container registry.
        /// </summary>
        [JsonPropertyName("Push"), JsonRequired]
        public bool Push { get; set; } = true;

        /// <summary>
        /// Specifies the packaging target to be deployed.
        /// </summary>
        [JsonPropertyName("Package"), JsonRequired]
        public BuildConfigProjectDeploymentPackage Package { get; set; } = new BuildConfigProjectDeploymentPackage();
    }
}
