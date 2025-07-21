namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Steam
{
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
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

        /// <summary>
        /// The Steam username to use for the deployment. If not set here, you must set the STEAM_USERNAME environment variable.
        /// </summary>
        [JsonPropertyName("SteamUsername")]
        public string? SteamUsername { get; set; }

        /// <summary>
        /// The path to the 'steamcmd.exe' executable, relative to the project root. Can be an absolute path instead. If not set here, you must set the STEAM_STEAMCMD_PATH environment variable.
        /// </summary>
        [JsonPropertyName("SteamCmdPath")]
        public string? SteamCmdPath { get; set; }
    }
}
