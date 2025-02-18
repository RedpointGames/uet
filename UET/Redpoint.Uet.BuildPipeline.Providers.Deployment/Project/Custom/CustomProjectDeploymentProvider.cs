namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Custom
{
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class CustomProjectDeploymentProvider : IProjectDeploymentProvider
    {
        public string Type => "Custom";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectDeploymentCustom;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> elements)
        {
            var castedSettings = elements
                .Select(x => (name: x.Name, manual: x.Manual ?? false, settings: (BuildConfigProjectDeploymentCustom)x.DynamicSettings))
                .ToList();

            foreach (var deployment in castedSettings)
            {
                string requiredFiles;
                if (deployment.settings.Packages != null)
                {
                    // Obtain staged files for all desired packages.
                    requiredFiles = deployment.settings.Packages.Aggregate("", (current, package) => current + $"#{package.Type}Staged_{package.Target}_{package.Platform}_{package.Configuration};") + "$(DynamicPreDeploymentNodes)";
                }
                else
                {
                    // If no packages are specified (also, for backwards compatibility), obtain all staged files.
                    requiredFiles = "$(GameStaged);$(ClientStaged);$(ServerStaged);$(DynamicPreDeploymentNodes)";
                }

                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Deployment {deployment.name}",
                        AgentType = deployment.manual ? "Win64_Manual" : "Win64",
                        NodeName = $"Deployment {deployment.name}",
                        Requires = requiredFiles,
                    },
                    async writer =>
                    {
                        await writer.WriteSpawnAsync(
                            new SpawnElementProperties
                            {
                                Exe = "powershell.exe",
                                Arguments = new[]
                                {
                                    "-ExecutionPolicy",
                                    "Bypass",
                                    "-File",
                                    $@"""$(RepositoryRoot)/{deployment.settings.ScriptPath}""",
                                    "-EnginePath",
                                    $@"""$(EnginePath)""",
                                    "-StageDirectory",
                                    $@"""$(StageDirectory)""",
                                }
                            }).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                await writer.WriteDynamicNodeAppendAsync(
                    new DynamicNodeAppendElementProperties
                    {
                        NodeName = $"Deployment {deployment.name}",
                    }).ConfigureAwait(false);
            }
        }
    }
}
