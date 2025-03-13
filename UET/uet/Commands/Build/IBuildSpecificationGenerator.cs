namespace UET.Commands.Build
{
    using Redpoint.Uet.BuildPipeline.Environment;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using UET.Commands.EngineSpec;

    internal interface IBuildSpecificationGenerator
    {
        Task<BuildSpecification> BuildConfigProjectToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigProject buildConfig,
            BuildConfigProjectDistribution distribution,
            string repositoryRoot,
            bool executeBuild,
            string[]? executeTests,
            string[]? executeDeployments,
            bool strictIncludes,
            bool localExecutor,
            string? alternateStagingDirectory);

        Task<BuildSpecification> BuildConfigPluginToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigPlugin buildConfig,
            BuildConfigPluginDistribution distribution,
            string repositoryRoot,
            bool executeBuild,
            string[]? executeTests,
            string[]? executeDeployments,
            bool strictIncludes,
            bool localExecutor,
            bool isPluginRooted,
            string? commandlinePluginVersionName,
            long? commandlinePluginVersionNumber,
            bool skipPackaging);

        Task<BuildSpecification> BuildConfigEngineToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigEngineDistribution distribution,
            CancellationToken cancellationToken);

        BuildSpecification ProjectPathSpecToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping,
            bool strictIncludes,
            string[] extraPlatforms,
            string? projectStagingDirectory);

        Task<BuildSpecification> PluginPathSpecToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping,
            bool strictIncludes,
            string[] extraPlatforms,
            BuildConfigPluginPackageType packageType,
            string? commandlinePluginVersionName,
            long? commandlinePluginVersionNumber);
    }
}
