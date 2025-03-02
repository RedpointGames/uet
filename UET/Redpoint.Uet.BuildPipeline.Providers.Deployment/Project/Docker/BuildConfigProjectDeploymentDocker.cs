namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Docker
{
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigProjectDeploymentDocker
    {
        /// <summary>
        /// The Docker image tag without the version component.
        /// </summary>
        [JsonPropertyName("Image"), JsonRequired]
        public string Image { get; set; } = string.Empty;

        /// <summary>
        /// If true, the image is pushed to the container registry. Defaults to 'true'.
        /// </summary>
        [JsonPropertyName("Push")]
        public bool Push { get; set; } = true;

        /// <summary>
        /// If true, debugging symbols will be kept in the container image. This makes the container image much larger. Defaults to 'false'.
        /// </summary>
        [JsonPropertyName("KeepSymbols")]
        public bool KeepSymbols { get; set; } = false;

        /// <summary>
        /// Specifies the packaging target to be deployed.
        /// </summary>
        [JsonPropertyName("Package"), JsonRequired]
        public BuildConfigProjectDeploymentPackage Package { get; set; } = new BuildConfigProjectDeploymentPackage();

        /// <summary>
        /// Specifies a list of Helm deployments to run after the image has been pushed.
        /// </summary>
        [JsonPropertyName("Helm")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used for JSON serialization.")]
        public BuildConfigProjectDeploymentDockerHelm[]? Helm { get; set; } = null;
    }
}
