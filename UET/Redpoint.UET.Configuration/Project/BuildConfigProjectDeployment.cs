namespace Redpoint.UET.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectDeployment
    {
        /// <summary>
        /// The name of the job/step as it would be displayed on a build server. This must be unique amongst all deployments defined.
        /// </summary>
        [JsonPropertyName("Name"), JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The type of deployment. One of "Steam" or "Custom".
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigProjectDeploymentType Type { get; set; }

        /// <summary>
        /// If set, this will be emitted as a manual job on build servers. Defaults to false.
        /// </summary>
        [JsonPropertyName("Manual"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Manual { get; set; }

        /// <summary>
        /// Specifies what is being deployed.
        /// </summary>
        [JsonPropertyName("Package"), JsonRequired]
        public BuildConfigProjectDeploymentPackage? Package { get; set; } = null;

        /// <summary>
        /// Specifies the settings for deployment to Steam.
        /// </summary>
        [JsonPropertyName("Steam"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectDeploymentSteam? Steam { get; set; }

        /// <summary>
        /// Specifies the settings for a custom deployment.
        /// </summary>
        [JsonPropertyName("Custom"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectDeploymentCustom? Custom { get; set; }
    }
}
