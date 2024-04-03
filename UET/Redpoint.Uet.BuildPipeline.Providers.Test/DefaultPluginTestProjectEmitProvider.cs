﻿namespace Redpoint.Uet.BuildPipeline.Providers.Test
{
    using Redpoint.Uet.BuildGraph;
    using System.Threading.Tasks;
    using System.Xml;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration;

    internal sealed class DefaultPluginTestProjectEmitProvider : IPluginTestProjectEmitProvider
    {
        // Pick a unique property prefix so we never conflict.
        private const string _propertyPrefix = "TestProject";

        // This used to be a configurable option in BuildGraph_Plugin.xml, but there's no point to setting
        // it at runtime and it was only every a variable for debugging purposes.
        private const string _testProjectName = "T";

        public string GetTestProjectUProjectFilePath(BuildConfigHostPlatform platform)
        {
            return $"$(TempPath)/{_testProjectName}{platform}/{_testProjectName}{platform}.uproject";
        }

        public string GetTestProjectDirectoryPath(BuildConfigHostPlatform platform)
        {
            return $"$(TempPath)/{_testProjectName}{platform}";
        }

        public string GetTestProjectTags(BuildConfigHostPlatform platform)
        {
            return $"$({_propertyPrefix}_ProjectCompiled_{platform});#{_propertyPrefix}_Project_{platform}";
        }

        public async Task EnsureTestProjectNodesArePresentAsync(IBuildGraphEmitContext context, XmlWriter writer)
        {
            await context.EmitOnceAsync("PluginTestProject", async () =>
            {
                // Generate nodes for all the platforms that we could possibly use on this executor. BuildGraph will only
                // execute the nodes that are actually needed by whatever called EnsureTestProjectNodesArePresentAsync.
                var allPlatforms = new[]
                {
                    BuildConfigHostPlatform.Win64,
                    BuildConfigHostPlatform.Mac
                }.Where(context.CanHostPlatformBeUsed).ToArray();

                // Create our "editor automation" packages. These packages only contain editor binaries, so
                // they're ready sooner than the full package (which will contain both editor and game binaries).
                foreach (var platform in allPlatforms)
                {
                    await writer.WritePropertyAsync(
                        new PropertyElementProperties
                        {
                            Name = $"{_propertyPrefix}_PluginEditorBinaries_{platform}",
                            Value = string.Empty,
                        }).ConfigureAwait(false);
                    await writer.WritePropertyAsync(
                        new PropertyElementProperties
                        {
                            Name = $"{_propertyPrefix}_PluginEditorBinaries_{platform}",
                            Value = $"#EditorBinaries_{platform}",
                            // @note: Plugin binaries are only available when not making a Marketplace package. For
                            // Marketplace packages, the plugin is compiled as part of the automation project prior to testing.
                            If = $"'$(IsForMarketplaceSubmission)' == 'false' and '$(CanBuildEditor{platform})' == 'true'"
                        }).ConfigureAwait(false);
                    await writer.WriteExpandAsync(
                        new ExpandElementProperties
                        {
                            Name = "Create Package",
                            Attributes =
                            {
                                { "AgentStage", $"Create Test Projects" },
                                { "AgentType", $"Win64" },
                                { "NodeName", $"Create {platform} Test Package" },
                                { "InputProject", "#HostProject" },
                                { "InputBaseDir", "$(TempPath)/$(HostProjectName)/Plugins/$(ShortPluginName)" },
                                { "InputBinaries", $"$({_propertyPrefix}_PluginEditorBinaries_{platform})" },
                                { "OutputDir", $"$(TempPath)/$(PackageFolder)-{platform}" },
                                { "OutputTag", $"#{_propertyPrefix}_Plugin_{platform}" },
                            },
                            If = platform == BuildConfigHostPlatform.Mac ? "'$(IsBuildMachine)' == 'true'" : null,
                        }).ConfigureAwait(false);
                }

                // Assemble our "editor automation" projects. These just contain the packages for the relevant
                // host platform. We potentially have two projects because one will have the Windows plugin
                // binaries and one will have the Mac binaries.
                foreach (var platform in allPlatforms)
                {
                    await writer.WriteExpandAsync(
                        new ExpandElementProperties
                        {
                            Name = "Assemble Test Project",
                            Attributes =
                            {
                                { "AgentStage", "Assemble Test Projects" },
                                { "AgentType", "Win64" },
                                { "NodeName", $"Assemble {platform} Test Project" },
                                { "AssembledProjectName", $"{_testProjectName}{platform}" },
                                { "InputFiles", $"#{_propertyPrefix}_Plugin_{platform}" },
                                { "InputPackageDir", $"$(TempPath)/$(PackageFolder)-{platform}" },
                                { "OutputTag", $"#{_propertyPrefix}_Project_{platform}" },
                                { "ProjectType", "BlueprintOnly" },
                            }
                        }).ConfigureAwait(false);
                }

                // Execute our editor automation tests.
                foreach (var platform in allPlatforms)
                {
                    var marketplaceCondition = $"'$(IsForMarketplaceSubmission)' == 'true' and '$(CanBuildEditor{platform})' == 'true'";
                    if (platform == BuildConfigHostPlatform.Mac)
                    {
                        marketplaceCondition += " and '$(IsBuildMachine)' == 'true'";
                    }

                    await writer.WriteAgentNodeAsync(
                        new AgentNodeElementProperties
                        {
                            AgentStage = $"Compile {platform} Test Project",
                            AgentType = platform.ToString(),
                            If = marketplaceCondition,
                            NodeName = $"Compile {platform} Test Project",
                            Requires = $"#{_propertyPrefix}_Project_{platform}",
                            Produces = $"#{_propertyPrefix}_ProjectCompiled_{platform}",
                        },
                        async writer =>
                        {
                            await writer.WriteExpandAsync(
                                new ExpandElementProperties
                                {
                                    Name = "RemoveStalePrecompiledHeaders",
                                    Attributes =
                                    {
                                        { "ProjectPath", GetTestProjectUProjectFilePath(platform) },
                                        { "TargetName", $"$(EnginePrefix)Editor" },
                                        { "TargetPlatform", platform.ToString() },
                                        { "TargetConfiguration", "Development" },
                                    }
                                }).ConfigureAwait(false);
                            await writer.WriteCompileAsync(
                                new CompileElementProperties
                                {
                                    Target = $"$(EnginePrefix)Editor",
                                    Platform = platform.ToString(),
                                    Configuration = "Development",
                                    Tag = $"#{_propertyPrefix}_ProjectCompiled_{platform}",
                                    Arguments = new[]
                                    {
                                        $@"-Project=""{GetTestProjectUProjectFilePath(platform)}""",
                                        $@"-plugin=""{GetTestProjectDirectoryPath(platform)}/Plugins/$(ShortPluginName)/$(PluginName).uplugin""",
                                        "-NoPDB",
                                        "-NoDebugInfo",
                                        "$(AdditionalArguments)",
                                    }
                                }).ConfigureAwait(false);
                        }).ConfigureAwait(false);

                    // @note: We use a property here because there won't be any compiled binaries when building
                    // for the Marketplace. This allows the nodes below to pull in compiled binaries only when
                    // they're available.
                    await writer.WritePropertyAsync(
                        new PropertyElementProperties
                        {
                            Name = $"{_propertyPrefix}_ProjectCompiled_{platform}",
                            Value = string.Empty,
                        }).ConfigureAwait(false);
                    await writer.WritePropertyAsync(
                        new PropertyElementProperties
                        {
                            Name = $"{_propertyPrefix}_ProjectCompiled_{platform}",
                            Value = $"#{_propertyPrefix}_ProjectCompiled_{platform}",
                            If = marketplaceCondition
                        }).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }
    }
}