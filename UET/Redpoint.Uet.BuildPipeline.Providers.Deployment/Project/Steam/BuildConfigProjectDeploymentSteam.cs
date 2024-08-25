namespace Redpoint.Uet.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectDeploymentSteam
    {
        /// <summary>
        /// The Steam app ID to deploy to.
        /// </summary>
        [JsonPropertyName("AppID"), JsonRequired]
        public string AppID { get; set; } = string.Empty;

        /// <summary>
        /// The Steam depot ID to deploy to.
        /// </summary>
        [JsonPropertyName("DepotID"), JsonRequired]
        public string DepotID { get; set; } = string.Empty;

        /// <summary>
        /// The Steam channel to deploy to.
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
