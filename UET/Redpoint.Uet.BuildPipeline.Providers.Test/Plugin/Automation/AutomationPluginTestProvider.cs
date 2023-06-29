namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Automation
{
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.BuildGraph;
    using System.Threading.Tasks;
    using System.Xml;
    using Redpoint.Uet.Configuration.Dynamic;
    using System.Text.Json;

    internal class AutomationPluginTestProvider : IPluginTestProvider
    {
        private readonly IPluginTestProjectEmitProvider _pluginTestProjectEmitProvider;
        private readonly IGlobalArgsProvider? _globalArgsProvider;

        public AutomationPluginTestProvider(
            IPluginTestProjectEmitProvider pluginTestProjectEmitProvider,
            IGlobalArgsProvider? globalArgsProvider = null)
        {
            _pluginTestProjectEmitProvider = pluginTestProjectEmitProvider;
            _globalArgsProvider = globalArgsProvider;
        }

        public string Type => "Automation";

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, TestProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginTestAutomation)!;
        }

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, ITestProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, settings: (BuildConfigPluginTestAutomation)x.DynamicSettings))
                .ToList();

            // Ensure we have the test project available.
            await _pluginTestProjectEmitProvider.EnsureTestProjectNodesArePresentAsync(
                context,
                writer);

            // Emit the nodes to run each test.
            var allPlatforms = castedSettings.SelectMany(x => x.settings.Platforms).Where(context.CanHostPlatformBeUsed).ToHashSet();
            foreach (var platform in allPlatforms)
            {
                await writer.WriteAgentAsync(
                    new AgentElementProperties
                    {
                        Name = $"Automation {platform} Tests",
                        Type = platform.ToString()
                    },
                    async writer =>
                    {
                        foreach (var test in castedSettings)
                        {
                            if (!test.settings.Platforms.Contains(platform))
                            {
                                continue;
                            }

                            var nodeName = $"Automation {platform} {test.name}";

                            await writer.WriteNodeAsync(
                                new NodeElementProperties
                                {
                                    Name = nodeName,
                                    Requires = _pluginTestProjectEmitProvider.GetTestProjectTags(platform),
                                    If = $"'$(CanBuildEditor{platform})' == 'true'"
                                },
                                async writer =>
                                {
                                    foreach (var configFile in test.settings.ConfigFiles ?? new string[0])
                                    {
                                        await writer.WriteCopyAsync(
                                            new CopyElementProperties
                                            {
                                                Files = "...",
                                                From = $"$(ProjectRoot)/{configFile}/",
                                                To = $"{_pluginTestProjectEmitProvider.GetTestProjectDirectoryPath(platform)}/Config/",
                                            });
                                    }

                                    var arguments = new List<string>();
                                    if (test.settings.MinWorkerCount != null)
                                    {
                                        arguments.AddRange(new string[]
                                        {
                                            "--min-worker-count",
                                            test.settings.MinWorkerCount.ToString()!
                                        });
                                    }
                                    if (test.settings.TestRunTimeoutMinutes != null)
                                    {
                                        arguments.AddRange(new string[]
                                        {
                                            "--test-run-timeout-minutes",
                                            test.settings.TestRunTimeoutMinutes.ToString()!
                                        });
                                    }
                                    if (test.settings.TestTimeoutMinutes != null)
                                    {
                                        arguments.AddRange(new string[]
                                        {
                                            "--test-timeout-minutes",
                                            test.settings.TestTimeoutMinutes.ToString()!
                                        });
                                    }
                                    if (test.settings.TestAttemptCount != null)
                                    {
                                        arguments.AddRange(new string[]
                                        {
                                            "--test-attempt-count",
                                            test.settings.TestAttemptCount.ToString()!
                                        });
                                    }

                                    await writer.WriteDeleteAsync(
                                        new DeleteElementProperties
                                        {
                                            Files = $"{_pluginTestProjectEmitProvider.GetTestProjectDirectoryPath(platform)}/TestResults_{platform}.xml"
                                        });
                                    await writer.WriteSpawnAsync(
                                        new SpawnElementProperties
                                        {
                                            Exe = "$(UETPath)",
                                            Arguments = (_globalArgsProvider?.GlobalArgsArray ?? new string[0]).Concat(new[]
                                            {
                                                "internal",
                                                "run-automation-test-from-buildgraph",
                                                "--engine-path",
                                                $@"""$(EnginePath)""",
                                                "--test-project-path",
                                                $@"""{_pluginTestProjectEmitProvider.GetTestProjectUProjectFilePath(platform)}""",
                                                "--test-prefix",
                                                test.settings.TestPrefix,
                                                "--test-results-path",
                                                $@"""$(ArtifactExportPath)/.uet/tmp/Automation{platform}/TestResults.xml""",
                                                "--worker-logs-path",
                                                $@"""$(ArtifactExportPath)/.uet/tmp/Automation{platform}"""
                                            }).Concat(arguments).ToArray()
                                        });
                                });
                            await writer.WriteDynamicNodeAppendAsync(
                                new DynamicNodeAppendElementProperties
                                {
                                    NodeName = nodeName,
                                    MustPassForLaterDeployment = true,
                                });
                        }
                    });
            }
        }
    }
}