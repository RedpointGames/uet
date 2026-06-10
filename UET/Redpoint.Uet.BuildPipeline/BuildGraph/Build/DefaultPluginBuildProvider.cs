namespace Redpoint.Uet.BuildPipeline.BuildGraph.Build
{
    using Redpoint.Collections;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Compile;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using System.Xml;

    internal class DefaultPluginBuildProvider : IPluginBuildProvider
    {
        private readonly IBuildGraphCompileGraphNodesGenerator _buildGraphCompileGraphNodesGenerator;

        public DefaultPluginBuildProvider(
            IBuildGraphCompileGraphNodesGenerator buildGraphCompileGraphNodesGenerator)
        {
            _buildGraphCompileGraphNodesGenerator = buildGraphCompileGraphNodesGenerator;
        }

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution)
        {
            var primaryHostPlatform = true switch
            {
                var v when v == OperatingSystem.IsWindows() => "Win64",
                var v when v == OperatingSystem.IsMacOS() => "Mac",
                var v when v == OperatingSystem.IsLinux() => "Linux",
                _ => "Win64",
            };

            var editorPlatformList = buildConfigDistribution.Build.Editor?.Platforms?.Select(x => x.ToString()).ToList() ?? [];
            if (editorPlatformList.Count == 0)
            {
                editorPlatformList.Add(primaryHostPlatform);
            }

            await _buildGraphCompileGraphNodesGenerator.WriteBuildGraphNodesToCompileAsync(
                context,
                writer,
                new CompilationContext
                {
                    UniqueName = "Plugin",
                    ProjectPath = "$(TempPath)/$(HostProjectName)",
                    StripPath = "$(TempPath)/$(HostProjectName)/Plugins/$(ShortPluginName)",
                    RunDynamicBeforeCompileMacrosVariable = "$(DynamicBeforeCompileMacros)",
                    Requires = ["#HostProject"],
                    ProductionCondition = async (productionResult) =>
                    {
                        switch (productionResult.HostPlatform)
                        {
                            case "Win64":
                                if (!context.CanHostPlatformBeUsed(Configuration.BuildConfigHostPlatform.Win64))
                                {
                                    return "false";
                                }
                                else
                                {
                                    return null;
                                }
                            case "Mac":
                                if (!context.CanHostPlatformBeUsed(Configuration.BuildConfigHostPlatform.Mac))
                                {
                                    return "false";
                                }
                                else
                                {
                                    return null;
                                }
                            default:
                                return null;
                        }
                    },
                    ActOnProductionTag = async (context, writer, productionResult) =>
                    {
                        await writer.WritePropertyAsync(
                            new PropertyElementProperties
                            {
                                Name = "EditorBinaries",
                                Value = $"$(EditorBinaries){productionResult.BinariesTag};",
                                // @todo: Does this need CanBuildEditorWin64/CanBuildEditorMac? ProductionCondition should already exclude it
                                If = "'$(PackageType)' == 'Generic' and '$(TargetType)' == 'Editor'",
                            });
                        await writer.WritePropertyAsync(
                            new PropertyElementProperties
                            {
                                Name = "GameBinaries",
                                Value = $"$(GameBinaries){productionResult.BinariesTag};",
                                If = "'$(PackageType)' == 'Generic' and '$(TargetType)' == 'Game'",
                            });
                    },
                    BuildTasksVariable = "BuildTasks",
                },
                [
                    new CompilationVector
                    {
                        Platforms = editorPlatformList,
                        Targets = [new("$(EnginePrefix)Editor", "Editor")],
                        Configurations = ["Development"],
                        Arguments =
                        [
                            "-Project=\"$(TempPath)/$(HostProjectName)/$(HostProjectName).uproject\"",
                            "-plugin=\"$(TempPath)/$(HostProjectName)/Plugins/$(ShortPluginName)/$(PluginName).uplugin\"",
                            "$(StripDebugFlags)",
                            "$(AdditionalArguments)",
                        ],
                        TagPrefix = "Plugin",
                    },
                    new CompilationVector
                    {
                        Platforms = buildConfigDistribution.Build.Game?.Platforms?.Select(x => x.Platform.ToString()).ToList() ?? [],
                        Targets = [new("$(EnginePrefix)Game", "Game")],
                        Configurations = buildConfigDistribution.Build.Game?.Configurations?.ToList() ?? [],
                        Arguments =
                        [
                            "-Project=\"$(TempPath)/$(HostProjectName)/$(HostProjectName).uproject\"",
                            "-plugin=\"$(TempPath)/$(HostProjectName)/Plugins/$(ShortPluginName)/$(PluginName).uplugin\"",
                            "$(StripDebugFlags)",
                            "$(AdditionalArguments)",
                        ],
                        TagPrefix = "Plugin",
                    },
                    new CompilationVector
                    {
                        Platforms = buildConfigDistribution.Build.Client?.Platforms?.Select(x => x.Platform.ToString()).ToList() ?? [],
                        Targets = [new("$(EnginePrefix)Client", "Client")],
                        Configurations = buildConfigDistribution.Build.Client?.Configurations?.ToList() ?? [],
                        Arguments =
                        [
                            "-Project=\"$(TempPath)/$(HostProjectName)/$(HostProjectName).uproject\"",
                            "-plugin=\"$(TempPath)/$(HostProjectName)/Plugins/$(ShortPluginName)/$(PluginName).uplugin\"",
                            "$(StripDebugFlags)",
                            "$(AdditionalArguments)",
                        ],
                        TagPrefix = "Plugin",
                    },
                    new CompilationVector
                    {
                        Platforms = buildConfigDistribution.Build.Server?.Platforms?.Select(x => x.Platform.ToString()).ToList() ?? [],
                        Targets = [new("$(EnginePrefix)Server", "Server")],
                        Configurations = buildConfigDistribution.Build.Server?.Configurations?.ToList() ?? [],
                        Arguments =
                        [
                            "-Project=\"$(TempPath)/$(HostProjectName)/$(HostProjectName).uproject\"",
                            "-plugin=\"$(TempPath)/$(HostProjectName)/Plugins/$(ShortPluginName)/$(PluginName).uplugin\"",
                            "$(StripDebugFlags)",
                            "$(AdditionalArguments)",
                        ],
                        TagPrefix = "Plugin",
                    },
                ]);

            await writer.WriteExpandAsync(
                new ExpandElementProperties
                {
                    Name = "Create Package",
                    Attributes =
                    {
                        { "AgentStage", "Create Final Package" },
                        { "AgentType", primaryHostPlatform },
                        { "NodeName", "Create Package" },
                        { "InputProject", "#HostProject" },
                        { "InputBaseDir", "$(TempPath)/$(HostProjectName)/Plugins/$(ShortPluginName)" },
                        { "InputBinaries", "$(EditorBinaries);$(GameBinaries)" },
                        { "OutputDir", "$(TempPath)/$(PackageFolder)" },
                        { "OutputLooseTag", "#PackagedPlugin" },
                        { "OutputZipTag", "#PackagedZip" },
                        { "ZipName", "$(ProjectRoot)/$(PluginName)-$(Distribution)-$(VersionName).zip" }
                    }
                });
            await writer.WritePropertyAsync(
                new PropertyElementProperties
                {
                    Name = "PackageTasks",
                    Value = "$(PackageTasks)#PackagedZip;",
                    If = "'$(PackageType)' != 'None'"
                });
        }
    }
}
