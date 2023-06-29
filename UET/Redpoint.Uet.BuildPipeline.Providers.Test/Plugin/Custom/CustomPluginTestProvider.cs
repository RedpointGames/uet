namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Custom
{
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.BuildGraph;
    using System.Threading.Tasks;
    using System.Xml;
    using Redpoint.Uet.Configuration.Dynamic;
    using System.Text.Json;

    internal class CustomPluginTestProvider : IPluginTestProvider
    {
        private readonly IPluginTestProjectEmitProvider _pluginTestProjectEmitProvider;

        public CustomPluginTestProvider(
            IPluginTestProjectEmitProvider pluginTestProjectEmitProvider)
        {
            _pluginTestProjectEmitProvider = pluginTestProjectEmitProvider;
        }

        public string Type => "Custom";

        public object DeserializeDynamicSettings(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize(ref reader, TestProviderSourceGenerationContext.WithStringEnum.BuildConfigPluginTestCustom)!;
        }

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, ITestProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, settings: (BuildConfigPluginTestCustom)x.DynamicSettings))
                .ToList();

            // Write nodes for custom tests that run against the test project.
            await WriteBuildGraphTestProjectNodesAsync(
                context,
                writer,
                castedSettings);

            // Write nodes for custom tests that run against the packaged plugin.
            await WriteBuildGraphPackagedPluginNodesAsync(
                context,
                writer,
                castedSettings);
        }

        private async Task WriteBuildGraphTestProjectNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            IEnumerable<(string name, BuildConfigPluginTestCustom settings)> dynamicSettings)
        {
            // If we have any tests that need to run against the test project, we need to ensure
            // the nodes for the test project are present.
            var customTestsAgainstTestProject = dynamicSettings
                .Where(x =>
                    x.settings.TestAgainst == BuildConfigPluginTestCustomTestAgainst.TestProject &&
                    x.settings.Platforms != null &&
                    x.settings.Platforms.Length > 0)
                .ToArray();
            if (customTestsAgainstTestProject.Length > 0)
            {
                await _pluginTestProjectEmitProvider.EnsureTestProjectNodesArePresentAsync(context, writer);
            }

            // Emit the nodes to run custom tests to run against the test project.
            var allPlatformsAgainstTestProject = customTestsAgainstTestProject.SelectMany(x => x.settings.Platforms).Where(context.CanHostPlatformBeUsed).ToHashSet();
            foreach (var platform in allPlatformsAgainstTestProject)
            {
                await writer.WriteAgentAsync(
                    new AgentElementProperties
                    {
                        Name = $"Custom {platform} Project Tests",
                        Type = platform.ToString()
                    },
                    async writer =>
                    {
                        foreach (var test in customTestsAgainstTestProject)
                        {
                            if (!test.settings.Platforms.Contains(platform))
                            {
                                continue;
                            }

                            var nodeName = $"Test {test.name} {platform}";

                            await writer.WriteNodeAsync(
                                new NodeElementProperties
                                {
                                    Name = nodeName,
                                    Requires = _pluginTestProjectEmitProvider.GetTestProjectTags(platform),
                                },
                                async writer =>
                                {
                                    await writer.WriteSpawnAsync(
                                        new SpawnElementProperties
                                        {
                                            Exe = platform == BuildConfigHostPlatform.Mac ? "pwsh" : "powershell.exe",
                                            Arguments = new[]
                                            {
                                                "-ExecutionPolicy",
                                                "Bypass",
                                                "-File",
                                                @$"""$(ProjectRoot)/{test.settings.ScriptPath}""",
                                                "-EnginePath",
                                                @$"""$(EnginePath)""",
                                                "-TestProjectPath",
                                                @$"""{_pluginTestProjectEmitProvider.GetTestProjectUProjectFilePath(platform)}""",
                                            }
                                        });
                                });
                            await writer.WriteDynamicNodeAppendAsync(
                                new DynamicNodeAppendElementProperties
                                {
                                    NodeName = nodeName,
                                });
                        }
                    });
            }
        }

        private async Task WriteBuildGraphPackagedPluginNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            IEnumerable<(string name, BuildConfigPluginTestCustom settings)> dynamicSettings)
        {
            // Figure out what tests we're running against the packaged plugin.
            var customTestsAgainstPackagedPlugin = dynamicSettings
                .Where(x =>
                    x.settings.TestAgainst == BuildConfigPluginTestCustomTestAgainst.PackagedPlugin &&
                    x.settings.Platforms != null &&
                    x.settings.Platforms.Length > 0)
                .ToArray();

            // Emit the nodes to run custom tests to run against the test project.
            var allPlatformsAgainstPackagedPlugin = customTestsAgainstPackagedPlugin.SelectMany(x => x.settings.Platforms).Where(context.CanHostPlatformBeUsed).ToHashSet();
            foreach (var platform in allPlatformsAgainstPackagedPlugin)
            {
                await writer.WriteAgentAsync(
                    new AgentElementProperties
                    {
                        Name = $"Custom {platform} Package Tests",
                        Type = platform.ToString()
                    },
                    async writer =>
                    {
                        foreach (var test in customTestsAgainstPackagedPlugin)
                        {
                            if (!test.settings.Platforms.Contains(platform))
                            {
                                continue;
                            }

                            var nodeName = $"Test {test.name} {platform}";

                            await writer.WriteNodeAsync(
                                new NodeElementProperties
                                {
                                    Name = nodeName,
                                    Requires = "#PackagedPlugin",
                                },
                                async writer =>
                                {
                                    await writer.WriteSpawnAsync(
                                        new SpawnElementProperties
                                        {
                                            Exe = platform == BuildConfigHostPlatform.Mac ? "pwsh" : "powershell.exe",
                                            Arguments = new[]
                                            {
                                                "-ExecutionPolicy",
                                                "Bypass",
                                                "-File",
                                                @$"""$(ProjectRoot)/{test.settings.ScriptPath}""",
                                                "-EnginePath",
                                                @$"""$(EnginePath)""",
                                                "-TempPath",
                                                @$"""$(TempPath)/""",
                                                "-PackagedPluginPath",
                                                @$"""$(TempPath)/$(PackageFolder)/""",
                                            }
                                        });
                                });
                            await writer.WriteDynamicNodeAppendAsync(
                                new DynamicNodeAppendElementProperties
                                {
                                    NodeName = nodeName,
                                });
                        }
                    });
            }
        }
    }
}