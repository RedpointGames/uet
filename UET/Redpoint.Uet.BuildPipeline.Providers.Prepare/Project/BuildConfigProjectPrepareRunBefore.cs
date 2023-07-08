namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project
{
    public enum BuildConfigProjectPrepareRunBefore
    {
        /// <summary>
        /// Run this script before BuildGraph is invoked by UET on each build job.
        /// </summary>
        BuildGraph,

        /// <summary>
        /// Run this script before each compilation step. These will be started from BuildGraph, but before anything is compiled. This script is passed "TargetType", "TargetName", "TargetPlatform" and "TargetConfiguration" parameters.
        /// </summary>
        Compile,

        /// <summary>
        /// Run this script before the binaries and cooked content are combined together into the staged version of the project.
        /// </summary>
        Stage,
    }
}
