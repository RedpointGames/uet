namespace Redpoint.Uet.BuildPipeline.BuildGraph.Build
{
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Compile;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System.Xml;

    internal class DefaultProjectBuildProvider : IProjectBuildProvider
    {
        private readonly IBuildGraphCompileGraphNodesGenerator _buildGraphCompileGraphNodesGenerator;

        public DefaultProjectBuildProvider(
            IBuildGraphCompileGraphNodesGenerator buildGraphCompileGraphNodesGenerator)
        {
            _buildGraphCompileGraphNodesGenerator = buildGraphCompileGraphNodesGenerator;
        }

        private static IEnumerable<string> NormalizePlatforms(IEnumerable<string> input)
        {
            return input.Select(x =>
            {
                if (x == "MetaQuest" || x == "GooglePlay")
                {
                    return "Android";
                }
                else
                {
                    return x;
                }
            });
        }

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            bool filterHostToCurrentPlatformOnly)
        {
            var primaryHostPlatform = filterHostToCurrentPlatformOnly
                ? true switch
                {
                    var v when v == OperatingSystem.IsWindows() => "Win64",
                    var v when v == OperatingSystem.IsMacOS() => "Mac",
                    var v when v == OperatingSystem.IsLinux() => "Linux",
                    _ => "Win64",
                }
                : "Win64";

            var gamePlatforms = NormalizePlatforms(buildConfigDistribution.Build.Game?.Platforms?.Select(x => x.Platform) ?? []).ToList();
            var clientPlatforms = NormalizePlatforms(buildConfigDistribution.Build.Client?.Platforms?.Select(x => x.Platform) ?? []).ToList();
            var serverPlatforms = NormalizePlatforms(buildConfigDistribution.Build.Server?.Platforms?.Select(x => x.Platform) ?? []).ToList();

            var targetPlatforms = new HashSet<string>(gamePlatforms.Concat(clientPlatforms).Concat(serverPlatforms));

            await writer.WritePropertyAsync(
                new PropertyElementProperties
                {
                    Name = "BuildToolBinaries",
                    Value = string.Empty,
                });
            await writer.WritePropertyAsync(
                new PropertyElementProperties
                {
                    Name = "EditorBinaries",
                    Value = string.Empty,
                });

            // @todo: Expose this setting somehow, like we currently do for plugins.
            await writer.WritePropertyAsync(
                new PropertyElementProperties
                {
                    Name = "StripDebugSymbols",
                    Value = "false",
                });

            // Compilation targets
            await writer.WriteDoAsync(
                new DoElementProperties
                {
                    If = "'$(ExecuteBuild)' == 'true'"
                },
                async writer =>
                {
                    await _buildGraphCompileGraphNodesGenerator.WriteBuildGraphNodesToCompileAsync(
                        context,
                        writer,
                        new CompilationContext
                        {
                            UniqueName = "Project",
                            ProjectPath = "$(ProjectRoot)",
                            StripPath = "$(ProjectRoot)",
                            RunDynamicBeforeCompileMacrosVariable = "$(DynamicBeforeCompileMacros)",
                            Requires = [],
                            ProductionCondition = async (productionResult) =>
                            {
                                switch (productionResult.HostPlatform)
                                {
                                    case "Win64":
                                        if (filterHostToCurrentPlatformOnly && !OperatingSystem.IsWindows())
                                        {
                                            return "false";
                                        }
                                        else
                                        {
                                            return "true";
                                        }
                                    case "Mac":
                                        if (filterHostToCurrentPlatformOnly && !OperatingSystem.IsMacOS())
                                        {
                                            return "false";
                                        }
                                        else
                                        {
                                            return "true";
                                        }
                                    case "Linux":
                                        if (filterHostToCurrentPlatformOnly && !OperatingSystem.IsLinux())
                                        {
                                            return "false";
                                        }
                                        else
                                        {
                                            return "true";
                                        }
                                    default:
                                        return null;
                                }
                            },
                            ActOnProductionTag = async (context, writer, productionResult) =>
                            {
                                if (productionResult.TagPrefix == "BuildToolBinaries")
                                {
                                    await writer.WritePropertyAsync(
                                        new PropertyElementProperties
                                        {
                                            Name = "BuildToolBinaries",
                                            Value = $"$(BuildToolBinaries){productionResult.BinariesTag};",
                                        });
                                }
                                else
                                {
                                    await writer.WritePropertyAsync(
                                        new PropertyElementProperties
                                        {
                                            Name = "EditorBinaries",
                                            Value = $"$(EditorBinaries){productionResult.BinariesTag};",
                                            If = $"'$(TargetPlatform)' == '{productionResult.HostPlatform}' and '$(TargetType)' == 'Editor'",
                                        });
                                    await writer.WritePropertyAsync(
                                        new PropertyElementProperties
                                        {
                                            Name = "$(TargetType)Binaries",
                                            Value = $"$($(TargetType)Binaries){productionResult.BinariesTag};",
                                            If = $"'$(TargetType)' != 'Editor'",
                                        });

                                    await writer.WritePropertyAsync(
                                        new PropertyElementProperties
                                        {
                                            Name = "VariantName",
                                            Value = "Compile_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                                        });
                                    await writer.WritePropertyAsync(
                                        new PropertyElementProperties
                                        {
                                            Name = "GeneratedVariantsList",
                                            Value = "$(GeneratedVariantsList)$(VariantName);",
                                            If = "!ContainsItem('$(GeneratedVariantsList)', '$(VariantName)', ';')",
                                        });
                                }
                            },
                            BuildTasksVariable = null,
                        },
                        [
                            new CompilationVector
                            {
                                Platforms = [primaryHostPlatform],
                                Targets =
                                [
                                    new("$(EditorTarget)", "Editor")
                                ],
                                Configurations = ["Development"],
                                Arguments =
                                [
                                    "-Project=\"$(UProjectPath)\"",
                                    "$(AdditionalArguments)",
                                ],
                                TagPrefix = "Project",
                            },
                            new CompilationVector
                            {
                                Platforms = [primaryHostPlatform],
                                Targets =
                                [
                                    new("UnrealPak", "Editor", "'$(IsUnrealEngineInstalled)' != 'true'"),
                                    new("ShaderCompileWorker", "Editor", "'$(IsUnrealEngineInstalled)' != 'true'"),
                                    new("ZenLaunch", "Editor", "'$(IsUnrealEngineInstalled)' != 'true' and '$(UET_ZEN_PUBLISH_HOST)' != ''"),
                                ],
                                Configurations = ["Development"],
                                Arguments =
                                [
                                    "-Project=\"$(UProjectPath)\"",
                                    "$(AdditionalArguments)",
                                ],
                                TagPrefix = "BuildToolBinaries",
                            },
                            new CompilationVector
                            {
                                Platforms = targetPlatforms.Where(x => x == "Win64" || x == "Mac" || x == "Linux").ToList(),
                                Targets =
                                [
                                    new("BootstrapPackagedGame", "Editor", "'$(IsUnrealEngineInstalled)' != 'true'"),
                                ],
                                Configurations = ["Shipping"],
                                Arguments =
                                [
                                    "-Project=\"$(UProjectPath)\"",
                                    "$(AdditionalArguments)",
                                ],
                                TagPrefix = "BuildToolBinaries",
                            },
                            new CompilationVector
                            {
                                Platforms = targetPlatforms.Where(x => x == "Win64" || x == "Mac" || x == "Linux").ToList(),
                                Targets =
                                [
                                    new("CrashReportClient", "Editor", "'$(IsUnrealEngineInstalled)' != 'true'"),
                                ],
                                Configurations = ["Shipping"],
                                Arguments =
                                [
                                    "$(AdditionalArguments)",
                                ],
                                TagPrefix = "BuildToolBinaries",
                            },
                            new CompilationVector
                            {
                                Platforms = gamePlatforms.ToList() ?? [],
                                Targets = (buildConfigDistribution.Build.Game?.Targets ?? []).Select(x => new CompilationVectorTarget(x, "Game")).ToList(),
                                Configurations = buildConfigDistribution.Build.Game?.Configurations?.ToList() ?? [],
                                Arguments =
                                [
                                    "-Project=\"$(UProjectPath)\"",
                                    "-SkipDeploy",
                                    "$(AdditionalArguments)",
                                ],
                                TagPrefix = "Project",
                            },
                            new CompilationVector
                            {
                                Platforms = clientPlatforms.ToList() ?? [],
                                Targets = (buildConfigDistribution.Build.Client?.Targets ?? []).Select(x => new CompilationVectorTarget(x, "Client")).ToList(),
                                Configurations = buildConfigDistribution.Build.Client?.Configurations?.ToList() ?? [],
                                Arguments =
                                [
                                    "-Project=\"$(UProjectPath)\"",
                                    "-SkipDeploy",
                                    "$(AdditionalArguments)",
                                ],
                                TagPrefix = "Project",
                            },
                            new CompilationVector
                            {
                                Platforms = serverPlatforms.ToList() ?? [],
                                Targets = (buildConfigDistribution.Build.Server?.Targets ?? []).Select(x => new CompilationVectorTarget(x, "Server")).ToList(),
                                Configurations = buildConfigDistribution.Build.Server?.Configurations?.ToList() ?? [],
                                Arguments =
                                [
                                    "-Project=\"$(UProjectPath)\"",
                                    "-SkipDeploy",
                                    "$(AdditionalArguments)",
                                ],
                                TagPrefix = "Project",
                            },
                        ]);
                });

            // Cook targets
            await writer.WriteDoAsync(
                new DoElementProperties
                {
                    If = "'$(ExecuteBuild)' == 'true'"
                },
                async writer =>
                {
                    foreach (var type in new[] { "Game", "Client", "Server" })
                    {
                        await writer.WriteForEachAsync(
                            new ForEachElementProperties
                            {
                                Name = "TargetStore",
                                Values = [$"$({type}TargetPlatforms)"]
                            },
                            async writer =>
                            {
                                await writer.WritePropertyAsync(
                                    new PropertyElementProperties
                                    {
                                        Name = "CookFlavors",
                                        Value = "NoCookFlavor"
                                    });
                                if (type != "Server")
                                {
                                    await writer.WritePropertyAsync(
                                        new PropertyElementProperties
                                        {
                                            Name = "CookFlavors",
                                            Value = $"$(Android{type}CookFlavors)",
                                            If = $"'$(TargetStore)' == 'Android' and '$(Android{type}CookFlavors)' != ''"
                                        });
                                    await writer.WritePropertyAsync(
                                        new PropertyElementProperties
                                        {
                                            Name = "CookFlavors",
                                            Value = $"$(Android{type}CookFlavors)",
                                            If = $"'$(TargetStore)' == 'MetaQuest' and '$(Android{type}CookFlavors)' != ''"
                                        });
                                    await writer.WritePropertyAsync(
                                        new PropertyElementProperties
                                        {
                                            Name = "CookFlavors",
                                            Value = $"$(Android{type}CookFlavors)",
                                            If = $"'$(TargetStore)' == 'GooglePlay' and '$(Android{type}CookFlavors)' != ''"
                                        });
                                }
                                await writer.WriteForEachAsync(
                                    new ForEachElementProperties
                                    {
                                        Name = "CookFlavor",
                                        Values = ["$(CookFlavors)"]
                                    },
                                    async writer =>
                                    {
                                        await writer.WriteExpandAsync(
                                            new ExpandElementProperties
                                            {
                                                Name = "CookVariant",
                                                Attributes =
                                                {
                                                    { "TargetType", type },
                                                    { "TargetStore", "$(TargetStore)" },
                                                    { "CookFlavor", "$(CookFlavor)" },
                                                }
                                            });
                                    });
                            });
                    }
                });

            // Packaging targets
            await writer.WriteDoAsync(
                new DoElementProperties
                {
                    If = "'$(ExecuteBuild)' == 'true'"
                },
                async writer =>
                {
                    foreach (var type in new[] { "Game", "Client", "Server" })
                    {
                        await writer.WriteForEachAsync(
                            new ForEachElementProperties
                            {
                                Name = "TargetName",
                                Values = [$"$({type}Targets)"]
                            },
                            async writer =>
                            {
                                await writer.WriteForEachAsync(
                                    new ForEachElementProperties
                                    {
                                        Name = "TargetStore",
                                        Values = [$"$({type}TargetPlatforms)"]
                                    },
                                    async writer =>
                                    {
                                        await writer.WriteForEachAsync(
                                            new ForEachElementProperties
                                            {
                                                Name = "TargetConfiguration",
                                                Values = [$"$({type}Configurations)"]
                                            },
                                            async writer =>
                                            {
                                                await writer.WritePropertyAsync(
                                                    new PropertyElementProperties
                                                    {
                                                        Name = "CookFlavors",
                                                        Value = "NoCookFlavor"
                                                    });
                                                if (type != "Server")
                                                {
                                                    await writer.WritePropertyAsync(
                                                        new PropertyElementProperties
                                                        {
                                                            Name = "CookFlavors",
                                                            Value = $"$(Android{type}CookFlavors)",
                                                            If = $"'$(TargetStore)' == 'Android' and '$(Android{type}CookFlavors)' != ''"
                                                        });
                                                    await writer.WritePropertyAsync(
                                                        new PropertyElementProperties
                                                        {
                                                            Name = "CookFlavors",
                                                            Value = $"$(Android{type}CookFlavors)",
                                                            If = $"'$(TargetStore)' == 'MetaQuest' and '$(Android{type}CookFlavors)' != ''"
                                                        });
                                                    await writer.WritePropertyAsync(
                                                        new PropertyElementProperties
                                                        {
                                                            Name = "CookFlavors",
                                                            Value = $"$(Android{type}CookFlavors)",
                                                            If = $"'$(TargetStore)' == 'GooglePlay' and '$(Android{type}CookFlavors)' != ''"
                                                        });
                                                }
                                                await writer.WriteForEachAsync(
                                                    new ForEachElementProperties
                                                    {
                                                        Name = "CookFlavor",
                                                        Values = ["$(CookFlavors)"]
                                                    },
                                                    async writer =>
                                                    {
                                                        await writer.WriteExpandAsync(
                                                            new ExpandElementProperties
                                                            {
                                                                Name = "PackageVariant",
                                                                Attributes =
                                                                {
                                                                    { "TargetType", type },
                                                                    { "TargetName", "$(TargetName)" },
                                                                    { "TargetStore", "$(TargetStore)" },
                                                                    { "TargetConfiguration", "$(TargetConfiguration)" },
                                                                    { "CookFlavor", "$(CookFlavor)" },
                                                                }
                                                            });
                                                    });
                                            });
                                    });
                            });
                    }
                });
        }
    }
}
