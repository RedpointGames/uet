namespace Redpoint.Uet.BuildPipeline.Executors.BuildServer
{
    public record class BuildServerJobAgent
    {
        /// <summary>
        /// The platform of the build agent.
        /// </summary>
        public required BuildServerJobPlatform Platform;

        /// <summary>
        /// Additional tags that are specific to the CI/CD system for running jobs on specific agents.
        /// </summary>
        public required string[] BuildMachineTags;

        /// <summary>
        /// If true, this build job is a manual build job.
        /// </summary>
        public required bool IsManual;
    }
}
