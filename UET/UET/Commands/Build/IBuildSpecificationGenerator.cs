namespace UET.Commands.Build
{
    using Redpoint.UET.BuildPipeline.Environment;
    using Redpoint.UET.BuildPipeline.Executors;
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
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
            bool localExecutor);

        BuildSpecification BuildConfigEngineToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildConfigEngineDistribution distribution);

        BuildSpecification ProjectPathSpecToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping,
            bool strictIncludes);

        Task<BuildSpecification> PluginPathSpecToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping,
            bool strictIncludes);
    }
}
