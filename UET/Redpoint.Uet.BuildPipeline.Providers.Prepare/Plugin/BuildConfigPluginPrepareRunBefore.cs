namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin
{
    public enum BuildConfigPluginPrepareRunBefore
    {
        /// <summary>
        /// Run this script before BuildGraph is invoked by UET on each build job.
        /// </summary>
        BuildGraph,

        /// <summary>
        /// Run this script after we've assembled the plugin folder, but before we've finalized tagging it's content. This can be used to add additional files to the plugin. This script is passed "PackagePath".
        /// </summary>
        AssembleFinalize,

        /// <summary>
        /// Run this script before each compilation step. These will be started from BuildGraph, but before anything is compiled. This script is passed "TargetType", "TargetName", "TargetPlatform" and "TargetConfiguration" parameters.
        /// </summary>
        Compile,
    }
}
