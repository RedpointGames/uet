namespace Redpoint.Uet.BuildPipeline.Providers.Test.Project.Custom
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class CustomProjectTestProvider : IProjectTestProvider
    {
        private readonly ILogger<CustomProjectTestProvider> _logger;
        private readonly IScriptExecutor _scriptExecutor;

        public CustomProjectTestProvider(
            ILogger<CustomProjectTestProvider> logger,
            IScriptExecutor scriptExecutor)
        {
            _logger = logger;
            _scriptExecutor = scriptExecutor;
        }

        public string Type => "Custom";

        public IRuntimeJson DynamicSettings { get; } = new TestProviderRuntimeJson(TestProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectTestCustom;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, ITestProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, settings: (BuildConfigProjectTestCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings)
            {
                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Custom {entry.name}",
                        AgentType = "Win64",
                        NodeName = $"Custom {entry.name}",
                        Requires = "$(GameStaged);$(ClientStaged);$(ServerStaged)",
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
                                    $@"""$(RepositoryRoot)/{entry.settings.ScriptPath}""",
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
                        NodeName = $"Custom {entry.name}",
                        MustPassForLaterDeployment = true,
                    }).ConfigureAwait(false);
            }
        }
    }
}