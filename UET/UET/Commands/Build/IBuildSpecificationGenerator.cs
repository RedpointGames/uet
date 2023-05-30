namespace UET.Commands.Build
{
    using Redpoint.UET.BuildPipeline.Executors;
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using UET.Commands.EngineSpec;

    internal interface IBuildSpecificationGenerator
    {
        BuildSpecification BuildConfigProjectToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildConfigProjectDistribution distribution,
            string repositoryRoot,
            bool executeBuild = true,
            bool strictIncludes = false,
            bool executeTests = false,
            bool executeDeployment = false);

        BuildSpecification BuildConfigPluginToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildConfigPluginDistribution distribution);

        BuildSpecification BuildConfigEngineToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildConfigEngineDistribution distribution);

        BuildSpecification ProjectPathSpecToBuildSpec(BuildEngineSpecification engineSpec, PathSpec pathSpec);

        BuildSpecification PluginPathSpecToBuildSpec(BuildEngineSpecification engineSpec, PathSpec pathSpec);
    }
}
