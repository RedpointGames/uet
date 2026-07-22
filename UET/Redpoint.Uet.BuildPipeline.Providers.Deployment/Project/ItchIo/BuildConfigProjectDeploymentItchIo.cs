namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.ItchIo
{
    using Google.Protobuf.Reflection;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Steam;
    using System.Reflection;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;

    internal class BuildConfigProjectDeploymentItchIo
    {
        /// <summary>
        /// The itch.io "user/game" to upload to.
        /// </summary>
        [JsonPropertyName("Project"), JsonRequired]
        public string Project { get; set; } = string.Empty;

        /// <summary>
        /// The itch.io channel to upload to.
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