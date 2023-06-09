namespace Redpoint.UET.BuildPipeline.Executors
{
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.Environment;

    public class BuildSpecification
    {
        public required BuildEngineSpecification Engine { get; init; }

        public required BuildGraphScriptSpecification BuildGraphScript { get; init; }

        public required string BuildGraphTarget { get; init; }

        public required Dictionary<string, string> BuildGraphSettings { get; init; }

        public required BuildGraphEnvironment BuildGraphEnvironment { get; init; }

        public Dictionary<string, string> BuildGraphSettingReplacements { get; init; } = new Dictionary<string, string>();

        /// <summary>
        /// The name of the distribution being built, if one is set.
        /// </summary>
        public string DistributionName { get; init; } = string.Empty;

        /// <summary>
        /// A list of custom preparation scripts to run on the build job before BuildGraph is invoked.
        /// </summary>
        public List<string> BuildGraphPreparationScripts { get; init; } = new List<string>();

        /// <summary>
        /// Some executors will create a local workspace for this path and then use that workspace
        /// as __REPOSITORY_ROOT__. Others will use this path directly as __REPOSITORY_ROOT__.
        /// </summary>
        public required string BuildGraphRepositoryRoot { get; init; }

        /// <summary>
        /// The path to the UET binary itself. This is used so that BuildGraph can re-enter back to UET to perform some internal tasks.
        /// </summary>
        public required string UETPath { get; init; }

        /// <summary>
        /// If set, specifies the environment variables that should apply to all build steps.
        /// </summary>
        public Dictionary<string, string>? GlobalEnvironmentVariables { get; init; }

        /// <summary>
        /// If this is building a project, this is the name of the folder the project is stored in. This is used by the
        /// physical workspace provider to heavily optimize 'git checkout' for Unreal projects.
        /// </summary>
        public required string? ProjectFolderName { get; init; }
    }
}
