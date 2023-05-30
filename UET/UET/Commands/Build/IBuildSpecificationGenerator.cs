﻿namespace UET.Commands.Build
{
    using Redpoint.UET.BuildPipeline.Environment;
    using Redpoint.UET.BuildPipeline.Executors;
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using UET.Commands.EngineSpec;

    internal interface IBuildSpecificationGenerator
    {
        BuildSpecification BuildConfigProjectToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigProjectDistribution distribution,
            string repositoryRoot,
            bool executeBuild,
            bool strictIncludes,
            bool executeTests,
            bool executeDeployment);

        BuildSpecification BuildConfigPluginToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildConfigPluginDistribution distribution);

        BuildSpecification BuildConfigEngineToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildConfigEngineDistribution distribution);

        BuildSpecification ProjectPathSpecToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping);

        BuildSpecification PluginPathSpecToBuildSpec(BuildEngineSpecification engineSpec, PathSpec pathSpec);
    }
}
