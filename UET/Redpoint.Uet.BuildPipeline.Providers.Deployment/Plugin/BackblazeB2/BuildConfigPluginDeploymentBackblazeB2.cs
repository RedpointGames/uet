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
        /// The environment variable that contains the folder prefix to use when uploading. This should be set on your build server and made available to build jobs.
        /// </summary>
        [JsonPropertyName("FolderPrefixEnvVar"), JsonRequired]
        public string FolderPrefixEnvVar { get; set; } = string.Empty;
    }
}
