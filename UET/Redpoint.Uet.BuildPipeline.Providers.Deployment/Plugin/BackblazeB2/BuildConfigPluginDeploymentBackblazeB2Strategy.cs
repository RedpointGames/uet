namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Plugin.BackblazeB2
{
    public enum BuildConfigPluginDeploymentBackblazeB2Strategy
    {
        /// <summary>
        /// Upload to the specified folder prefix. Older uploads are not automatically removed.
        /// </summary>
        Continuous,

        /// <summary>
        /// Upload to an engine version folder under a channel folder under the folder prefix. The previously uploaded file for the channel and engine version is removed afterwards.
        /// 
        /// The engine version is determined based on the EngineVersion value in the .uplugin file.
        /// 
        /// The channel folder is determined by the environment variable UET_CHANNEL_NAME, CI_COMMIT_TAG, the default channel name set in the BuildConfig.json or "BleedingEdge" if none is specified.
        /// </summary>
        Channel,
    }
}
