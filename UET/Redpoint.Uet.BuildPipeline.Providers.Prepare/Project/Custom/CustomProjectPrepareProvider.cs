namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom
{
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using System.Xml;

    internal class CustomProjectPrepareProvider : IProjectPrepareProvider
    {
        private readonly IScriptExecutor _scriptExecutor;

        public CustomProjectPrepareProvider(
            IScriptExecutor scriptExecutor)
        {
            _scriptExecutor = scriptExecutor;
        }

        public string Type => "Custom";

        public JsonTypeInfo DynamicSettingsJsonTypeInfo => PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectPrepareCustom;

        public JsonSerializerContext DynamicSettingsJsonTypeInfoResolver => PrepareProviderSourceGenerationContext.WithStringEnum;

        public object DeserializeDynamicSettings(
            ref Utf8JsonReader reader,
            JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, PrepareProviderSourceGenerationContext.WithStringEnum.BuildConfigProjectPrepareCustom)!;
        }

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigProjectPrepareCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings)
            {
                foreach (var runBefore in entry.settings.RunBefore ?? Array.Empty<BuildConfigProjectPrepareRunBefore>())
                {
                    switch (runBefore)
                    {
                        case BuildConfigProjectPrepareRunBefore.Compile:
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
                                                $@"""$(RepositoryRoot)/{entry.settings.ScriptPath}""",
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
                                        });
                                    await writer.WriteSpawnAsync(
                                        new SpawnElementProperties
                                        {
                                            Exe = "pwsh",
                                            Arguments = new[]
                                            {
                                                "-ExecutionPolicy",
                                                "Bypass",
                                                $@"""$(RepositoryRoot)/{entry.settings.ScriptPath}""",
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
                                        });
                                });
                            await writer.WritePropertyAsync(
                                new PropertyElementProperties
                                {
                                    Name = "DynamicBeforeCompileMacros",
                                    Value = $"$(DynamicBeforeCompileMacros)CustomOnCompile-{entry.name};",
                                });
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
            }
        }

        public async Task RunBeforeBuildGraphAsync(
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries,
            string repositoryRoot,
            CancellationToken cancellationToken)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigProjectPrepareCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings
                .Where(x => (x.settings.RunBefore ?? Array.Empty<BuildConfigProjectPrepareRunBefore>()).Contains(BuildConfigProjectPrepareRunBefore.BuildGraph)))
            {
                await _scriptExecutor.ExecutePowerShellAsync(
                    new ScriptSpecification
                    {
                        ScriptPath = entry.settings.ScriptPath,
                        Arguments = Array.Empty<string>(),
                        WorkingDirectory = repositoryRoot,
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
            }
        }
    }
}
