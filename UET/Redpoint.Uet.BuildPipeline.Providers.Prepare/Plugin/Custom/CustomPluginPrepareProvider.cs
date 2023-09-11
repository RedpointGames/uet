namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin.Custom
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal class CustomPluginPrepareProvider : IPluginPrepareProvider
    {
        private readonly ILogger<CustomPluginPrepareProvider> _logger;
        private readonly IScriptExecutor _scriptExecutor;

        public CustomPluginPrepareProvider(
            ILogger<CustomPluginPrepareProvider> logger,
            IScriptExecutor scriptExecutor)
        {
            _logger = logger;
            _scriptExecutor = scriptExecutor;
        }

        public string Type => "Custom";

        public IRuntimeJson DynamicSettings { get; } = new PrepareProviderRuntimeJson(PrepareProviderSourceGenerationContext.WithStringEnum).BuildConfigPluginPrepareCustom;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>> entries)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigPluginPrepareCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings)
            {
                foreach (var runBefore in entry.settings.RunBefore ?? Array.Empty<BuildConfigPluginPrepareRunBefore>())
                {
                    switch (runBefore)
                    {
                        case BuildConfigPluginPrepareRunBefore.AssembleFinalize:
                            await writer.WriteMacroAsync(
                                new MacroElementProperties
                                {
                                    Name = $"CustomOnAssembleFinalize-{entry.name}",
                                    Arguments = new[]
                                    {
                                        "PackagePath",
                                    },
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
                                                $@"""$(ProjectRoot)/{entry.settings.ScriptPath}""",
                                                "-PackagePath",
                                                @"""$(PackagePath)""",
                                            }
                                        }).ConfigureAwait(false);
                                }).ConfigureAwait(false);
                            await writer.WritePropertyAsync(
                                new PropertyElementProperties
                                {
                                    Name = "DynamicBeforeAssembleFinalizeMacros",
                                    Value = $"$(DynamicBeforeAssembleFinalizeMacros)CustomOnAssembleFinalize-{entry.name};",
                                }).ConfigureAwait(false);
                            break;
                        case BuildConfigPluginPrepareRunBefore.Compile:
                            await writer.WriteMacroAsync(
                                new MacroElementProperties
                                {
                                    Name = $"CustomOnCompile-{entry.name}",
                                    Arguments = new[]
                                    {
                                        "TargetType",
                                        "TargetName",
                                        "TargetPlatform",
                                        "TargetConfiguration",
                                        "HostPlatform",
                                    }
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
                                                $@"""$(ProjectRoot)/{entry.settings.ScriptPath}""",
                                                "-TargetType",
                                                @"""$(TargetType)""",
                                                "-TargetName",
                                                @"""$(TargetName)""",
                                                "-TargetPlatform",
                                                @"""$(TargetPlatform)""",
                                                "-TargetConfiguration",
                                                @"""$(TargetConfiguration)""",
                                            },
                                            If = "$(HostPlatform) == 'Win64'"
                                        }).ConfigureAwait(false);
                                    await writer.WriteSpawnAsync(
                                        new SpawnElementProperties
                                        {
                                            Exe = "pwsh",
                                            Arguments = new[]
                                            {
                                                "-ExecutionPolicy",
                                                "Bypass",
                                                $@"""$(ProjectRoot)/{entry.settings.ScriptPath}""",
                                                "-TargetType",
                                                @"""$(TargetType)""",
                                                "-TargetName",
                                                @"""$(TargetName)""",
                                                "-TargetPlatform",
                                                @"""$(TargetPlatform)""",
                                                "-TargetConfiguration",
                                                @"""$(TargetConfiguration)""",
                                            },
                                            If = "$(HostPlatform) == 'Mac'"
                                        }).ConfigureAwait(false);
                                }).ConfigureAwait(false);
                            await writer.WritePropertyAsync(
                                new PropertyElementProperties
                                {
                                    Name = "DynamicBeforeCompileMacros",
                                    Value = $"$(DynamicBeforeCompileMacros)CustomOnCompile-{entry.name};",
                                }).ConfigureAwait(false);
                            break;
                        case BuildConfigPluginPrepareRunBefore.BuildGraph:
                            // We don't emit anything in the graph for these.
                            break;
                        default:
                            throw new NotSupportedException($"The RunBefore type of '{runBefore}' is not supported for CustomPluginPrepareProvider.");
                    }
                }
            }
        }

        public async Task RunBeforeBuildGraphAsync(
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>> entries,
            string repositoryRoot, CancellationToken cancellationToken)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigPluginPrepareCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings
                .Where(x => (x.settings.RunBefore ?? Array.Empty<BuildConfigPluginPrepareRunBefore>()).Contains(BuildConfigPluginPrepareRunBefore.BuildGraph)))
            {
                _logger.LogInformation($"Executing pre-BuildGraph custom preparation step '{entry.name}': '{entry.settings.ScriptPath}'");
                await _scriptExecutor.ExecutePowerShellAsync(
                    new ScriptSpecification
                    {
                        ScriptPath = entry.settings.ScriptPath,
                        Arguments = Array.Empty<string>(),
                        WorkingDirectory = repositoryRoot,
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
