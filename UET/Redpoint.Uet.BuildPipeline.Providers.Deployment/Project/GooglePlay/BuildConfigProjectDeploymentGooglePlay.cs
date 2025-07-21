namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.GooglePlay
{
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using System.Text.Json.Serialization;

    internal class BuildConfigProjectDeploymentGooglePlay
    {
        /// <summary>
        /// The package name to deploy to.
        /// </summary>
        [JsonPropertyName("PackageName"), JsonRequired]
        public required string PackageName { get; set; }

        /// <summary>
        /// One or more tracks to deploy to; entries must be one of 'production', 'beta', 'alpha' or 'internal'. Defaults to just 'internal'.
        /// </summary>
        [JsonPropertyName("Tracks")]
        public string[] Tracks { get; set; } = ["internal"];

        /// <summary>
        /// The path to the JSON key used to authenticate with the Google Play Developer Console. This should be set up according to Fastlane instructions: https://docs.fastlane.tools/getting-started/android/setup/#collect-your-google-credentials
        /// 
        /// This path is relative to the project root. It can also be an absolute path.
        /// 
        /// If you set the UET_GOOGLE_PLAY_JSON_KEY_PATH environment variable, it will override this setting. If you don't set this setting, you must provide set the UET_GOOGLE_PLAY_JSON_KEY_PATH environment variable.
        /// </summary>
        [JsonPropertyName("JsonKeyPath")]
        public string? JsonKeyPath { get; set; } = null;

        /// <summary>
        /// Specifies the packaging target to be deployed.
        /// </summary>
        [JsonPropertyName("Package"), JsonRequired]
        public BuildConfigProjectDeploymentPackage Package { get; set; } = new BuildConfigProjectDeploymentPackage();
    }
}
