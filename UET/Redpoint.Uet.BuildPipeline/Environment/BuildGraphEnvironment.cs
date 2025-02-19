namespace Redpoint.Uet.BuildPipeline.Environment
{
    /// <summary>
    /// BuildGraphEnvironment encapsulates all of the environment settings that surround
    /// how BuildGraph jobs are run on build server agents.
    /// </summary>
    public record BuildGraphEnvironment
    {
        /// <summary>
        /// The pipeline ID of the build on your build server. On GitLab, this is the CI_PIPELINE_ID environment variable.
        /// </summary>
        public required string PipelineId { get; init; }

        /// <summary>
        /// The Windows build environment.
        /// </summary>
        public required BuildGraphWindowsEnvironment Windows { get; init; }

        /// <summary>
        /// The macOS build environment.
        /// </summary>
        public BuildGraphMacEnvironment? Mac { get; init; } = null;
    }
}
