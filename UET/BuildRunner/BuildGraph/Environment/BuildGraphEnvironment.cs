namespace BuildRunner.BuildGraph.Environment
{
    /// <summary>
    /// BuildGraphEnvironment encapsulates all of the environment settings that surround
    /// how BuildGraph jobs are run on build server agents.
    /// </summary>
    internal record BuildGraphEnvironment
    {
        /// <summary>
        /// The engine version to build against. This will be null for custom Unreal Engine builds.
        /// </summary>
        public required string? Engine { get; init; }

        /// <summary>
        /// If true, this is building a custom Unreal Engine install, not a project or plugin.
        /// </summary>
        public required bool IsEngineBuild { get; init; }

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
        public required BuildGraphMacEnvironment Mac { get; init; }
    }
}
