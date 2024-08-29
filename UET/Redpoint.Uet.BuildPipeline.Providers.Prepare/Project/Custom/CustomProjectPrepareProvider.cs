namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin.Custom;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class CustomProjectPrepareProvider : IProjectPrepareProvider
    {
        private readonly ILogger<CustomPluginPrepareProvider> _logger;
        private readonly IScriptExecutor _scriptExecutor;

        public CustomProjectPrepareProvider(
            ILogger<CustomPluginPrepareProvider> logger,
            IScriptExecutor scriptExecutor)
        {
            _logger = logger;
            _scriptExecutor = scriptExecutor;
        }

        public string Type => "Custom";

        public IRuntimeJson DynamicSettings { get; } = new PrepareProviderRuntimeJson(PrepareProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectPrepareCustom;

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
                                                "-File",
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
                                        }).ConfigureAwait(false);
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
                                        }).ConfigureAwait(false);
                                }).ConfigureAwait(false);
                            await writer.WritePropertyAsync(
                                new PropertyElementProperties
                                {
                                    Name = "DynamicBeforeCompileMacros",
                                    Value = $"$(DynamicBeforeCompileMacros)CustomOnCompile-{entry.name};",
                                }).ConfigureAwait(false);
                            break;
                        case BuildConfigProjectPrepareRunBefore.BuildGraph:
                            // We don't emit anything in the graph for these.
                            break;
                        default:
                            throw new NotSupportedException($"The RunBefore type of '{runBefore}' is not supported for CustomProjectPrepareProvider.");
                    }
                }
            }
        }

        public async Task<int> RunBeforeBuildGraphAsync(
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>> entries,
            string repositoryRoot,
            IReadOnlyDictionary<string, string> preBuildGraphArguments,
            CancellationToken cancellationToken)
        {
            var castedSettings = entries
                .Select(x => (name: x.Name, settings: (BuildConfigProjectPrepareCustom)x.DynamicSettings))
                .ToList();

            foreach (var entry in castedSettings
                .Where(x => (x.settings.RunBefore ?? Array.Empty<BuildConfigProjectPrepareRunBefore>()).Contains(BuildConfigProjectPrepareRunBefore.BuildGraph)))
            {
                var scriptPath = Path.Combine(repositoryRoot, entry.settings.ScriptPath);
                if (!File.Exists(scriptPath))
                {
                    _logger.LogError($"Unable to locate script at expected path: '{scriptPath}'");
                    return 1;
                }

                var arguments = new List<LogicalProcessArgument>();
                foreach (var argument in entry.settings.ScriptArguments ?? [])
                {
                    arguments.Add(argument);
                }

                _logger.LogInformation($"Executing pre-BuildGraph custom preparation step '{entry.name}' in directory '{repositoryRoot}': '{scriptPath}'");
                var exitCode = await _scriptExecutor.ExecutePowerShellAsync(
                    new ScriptSpecification
                    {
                        ScriptPath = scriptPath,
                        Arguments = arguments,
                        WorkingDirectory = repositoryRoot,
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    return exitCode;
                }
            }

            return 0;
        }
    }
}
