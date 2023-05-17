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

        public required string BuildGraphLocalArtifactPath { get; init; }
    }
}
