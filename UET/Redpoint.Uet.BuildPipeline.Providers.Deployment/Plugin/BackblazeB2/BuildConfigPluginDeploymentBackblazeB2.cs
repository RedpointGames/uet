namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginDeploymentBackblazeB2
    {
        /// <summary>
        /// The Backblaze B2 bucket to upload to.
        /// </summary>
        [JsonPropertyName("BucketName"), JsonRequired]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// The folder prefix to use when uploading. Either this or 'FolderPrefixEnvVar' must be set.
        /// </summary>
        [JsonPropertyName("FolderPrefix")]
        public string FolderPrefix { get; set; } = string.Empty;

        /// <summary>
        /// The environment variable that contains the folder prefix to use when uploading. Either this or 'FolderPrefix' must be set. This should be set on your build server and made available to build jobs.
        /// </summary>
        [JsonPropertyName("FolderPrefixEnvVar")]
        public string FolderPrefixEnvVar { get; set; } = string.Empty;

        /// <summary>
        /// The strategy to use for the upload.
        /// </summary>
        [JsonPropertyName("Strategy")]
        public BuildConfigPluginDeploymentBackblazeB2Strategy Strategy { get; set; } = BuildConfigPluginDeploymentBackblazeB2Strategy.Continuous;

        /// <summary>
        /// The default channel name used for the channel upload strategy 
        /// </summary>
        [JsonPropertyName("DefaultChannelName")]
        public string DefaultChannelName { get; set; } = string.Empty;
    }
}
