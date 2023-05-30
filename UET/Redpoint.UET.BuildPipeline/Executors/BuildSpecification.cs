namespace Redpoint.UET.BuildPipeline.Executors
{
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.Environment;

    public class BuildSpecification
    {
        public required BuildEngineSpecification Engine { get; init; }

        public required BuildGraphScriptSpecification BuildGraphScript { get; init; }

        public required string BuildGraphTarget { get; init; }

        public required BuildGraphSettings BuildGraphSettings { get; init; }

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
    }
}
