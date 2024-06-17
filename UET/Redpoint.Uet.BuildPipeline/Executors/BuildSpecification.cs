namespace Redpoint.Uet.BuildPipeline.Executors
{
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Environment;
    using Redpoint.Uet.BuildPipeline.MultiWorkspace;
    using Redpoint.Uet.Configuration.Engine;

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

        public required IReadOnlyDictionary<string, MultiWorkspaceDescriptor> WorkspaceDescriptors { get; init; }

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

        /// <summary>
        /// The root path under which to export artifacts such as test results. This should always be set to the
        /// working directory of the original command, regardless of workspace virtualisation, so that build servers
        /// can save artifacts.
        /// </summary>
        public required string ArtifactExportPath { get; init; }

        /// <summary>
        /// The mobile provisions to apply before BuildGraph starts. For builds that run on a build server, these files will be staged to shared storage.
        /// </summary>
        public required IReadOnlyList<BuildConfigMobileProvision> MobileProvisions { get; set; }
    }
}
