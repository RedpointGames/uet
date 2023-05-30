namespace Redpoint.UET.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginDeployment
    {
        /// <summary>
        /// The name of the job/step as it would be displayed on a build server. This must be unique amongst all deployments defined.
        /// </summary>
        [JsonPropertyName("Name"), JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The type of deployment. Currently the only supported value is "BackblazeB2".
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigPluginDeploymentType Type { get; set; }

        /// <summary>
        /// If set, this will be emitted as a manual job on build servers. Defaults to false.
        /// </summary>
        [JsonPropertyName("Manual"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Manual { get; set; }

        /// <summary>
        /// Specifies the settings for deployment to Backblaze B2.
        /// </summary>
        [JsonPropertyName("BackblazeB2"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginDeploymentBackblazeB2? BackblazeB2 { get; set; }
    }
}
