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
            BuildConfigProjectDistribution distribution,
            string repositoryRoot,
            bool executeBuild,
            bool executeTests,
            bool executeDeployment,
            bool strictIncludes,
            bool localExecutor);

        Task<BuildSpecification> BuildConfigPluginToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigPluginDistribution distribution,
            BuildConfigPlugin pluginInfo,
            string repositoryRoot,
            bool executeBuild,
            bool executePackage,
            bool executeTests,
            bool executeDeployment,
            bool strictIncludes,
            bool localExecutor,
            bool isPluginRooted,
            string? commandlinePluginVersionName,
            long? commandlinePluginVersionNumber);

        BuildSpecification BuildConfigEngineToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildConfigEngineDistribution distribution);

        BuildSpecification ProjectPathSpecToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping,
            bool strictIncludes,
            string[] extraPlatforms);

        Task<BuildSpecification> PluginPathSpecToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping,
            bool strictIncludes,
            string[] extraPlatforms,
            bool package,
            bool marketplace,
            string? commandlinePluginVersionName,
            long? commandlinePluginVersionNumber);
    }
}
