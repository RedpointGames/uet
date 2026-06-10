namespace Redpoint.Uet.BuildPipeline.BuildGraph.Compile
{
    using Redpoint.Hashing;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.Configuration.Dynamic;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Xml;

    internal class DefaultBuildGraphCompileGraphNodesGenerator : IBuildGraphCompileGraphNodesGenerator
    {
        private static CompilationProductionResult GetCompilationProductionResult(string tagPrefix, string hostPlatform)
        {
            return new CompilationProductionResult
            {
                BinariesTag = $"#{tagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                ReceiptsTag = $"#{tagPrefix}_Receipts_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                TagPrefix = tagPrefix,
                TargetTypeExpression = "$(TargetType)",
                TargetNameExpression = "$(TargetName)",
                TargetPlatformExpression = "$(TargetPlatform)",
                TargetConfigurationExpression = "$(TargetConfiguration)",
                HostPlatform = hostPlatform,
            };
        }

        private static async Task WriteVectorLoopAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            CompilationContext compilation,
            string hostPlatform,
            CompilationVector vector,
            Func<XmlWriter, Task> writeChildren)
        {
            // @note: We don't flatten ForEach here in case the compilation vector contains variables that need to be expanded.

            if (vector.Targets.Count == 0 ||
                vector.Platforms.Count == 0 ||
                vector.Configurations.Count == 0)
            {
                return;
            }

            var productionResult = GetCompilationProductionResult(vector.TagPrefix, hostPlatform);
            var ifCondition = compilation.ProductionCondition == null
                ? null
                : await compilation.ProductionCondition(productionResult);

            await writer.WriteForEachAsync(
                new ForEachElementProperties
                {
                    Name = "TargetPlatform",
                    Values = vector.Platforms,
                },
                async writer =>
                {
                    await writer.WriteForEachAsync(
                        new ForEachElementProperties
                        {
                            Name = "TargetNameAndType",
                            Values = vector.Targets.Select(x => $"{x.TargetName}|{x.TargetType}").ToArray(),
                            If = hostPlatform == "Win64"
                                ? $"!ContainsItem('$(MacPlatforms)', '$(TargetPlatform)', ';')"
                                : $"ContainsItem('$(MacPlatforms)', '$(TargetPlatform)', ';')"
                        },
                        async writer =>
                        {
                            await writer.WriteStringOpAsync(
                                new StringOpElementProperties
                                {
                                    Input = "$(TargetNameAndType)",
                                    Method = "SplitFirst",
                                    Output = "TargetName",
                                    Arguments = ["|"]
                                });
                            await writer.WriteStringOpAsync(
                                new StringOpElementProperties
                                {
                                    Input = "$(TargetNameAndType)",
                                    Method = "SplitLast",
                                    Output = "TargetType",
                                    Arguments = ["|"]
                                });
                            await writer.WriteForEachAsync(
                                new ForEachElementProperties
                                {
                                    Name = "TargetConfiguration",
                                    Values = vector.Configurations,
                                },
                                async writer =>
                                {
                                    if (ifCondition == null)
                                    {
                                        await writeChildren(writer);
                                    }
                                    else
                                    {
                                        await writer.WriteDoAsync(
                                            new DoElementProperties
                                            {
                                                If = ifCondition,
                                            },
                                            writeChildren);
                                    }
                                });
                        });
                });
        }

        private static async Task WriteUnifiedCompileAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            CompilationContext compilation,
            string hostPlatform,
            IReadOnlyList<CompilationVector> vectors)
        {
            var hash = Hash.Sha256AsHexString(compilation.UniqueName + '|' + hostPlatform, Encoding.UTF8);
            var tempProduces = $"Temp_Produces_{hash}";
            var platformsForThisHost = $"Temp_TargetPlatforms_{hash}";
            await writer.WritePropertyAsync(
                new PropertyElementProperties
                {
                    Name = tempProduces,
                    Value = "",
                });
            await writer.WritePropertyAsync(
                new PropertyElementProperties
                {
                    Name = platformsForThisHost,
                    Value = "",
                });
            foreach (var vector in vectors)
            {
                await WriteVectorLoopAsync(
                    context,
                    writer,
                    compilation,
                    hostPlatform,
                    vector,
                    async writer =>
                    {
                        await writer.WritePropertyAsync(
                            new PropertyElementProperties
                            {
                                Name = tempProduces,
                                Value = $"$({tempProduces})#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration);#{vector.TagPrefix}_Receipts_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration);",
                            });
                        await writer.WritePropertyAsync(
                            new PropertyElementProperties
                            {
                                Name = platformsForThisHost,
                                Value = $"$({platformsForThisHost}) $(TargetPlatform)",
                                If = $"!ContainsItem('$({platformsForThisHost})', '$(TargetPlatform)', ' ')"
                            });
                        await compilation.ActOnProductionTag(
                            context,
                            writer,
                            GetCompilationProductionResult(vector.TagPrefix, hostPlatform));
                    });
            }

            await writer.WriteDoAsync(
                new DoElementProperties
                {
                    If = $"'$({tempProduces})' != '' and '$({platformsForThisHost})' != ''"
                },
                async writer =>
                {
                    if (compilation.BuildTasksVariable != null)
                    {
                        await writer.WritePropertyAsync(
                            new PropertyElementProperties
                            {
                                Name = compilation.BuildTasksVariable,
                                Value = $"$({compilation.BuildTasksVariable})Compile {compilation.UniqueName}$({platformsForThisHost});"
                            });
                    }

                    await writer.WriteAgentNodeAsync(
                        new AgentNodeElementProperties
                        {
                            AgentName = $"Compile {compilation.UniqueName} {hostPlatform}",
                            AgentType = hostPlatform,
                            AgentStage = string.Empty,
                            NodeName = $"Compile {compilation.UniqueName}$({platformsForThisHost})",
                            Requires = string.Join(';', compilation.Requires),
                            Produces = $"$({tempProduces})",
                        },
                        async writer =>
                        {
                            if (compilation.RunDynamicBeforeCompileMacrosVariable != null)
                            {
                                await writer.WriteForEachAsync(
                                    new ForEachElementProperties
                                    {
                                        Name = "MacroName",
                                        Values = [compilation.RunDynamicBeforeCompileMacrosVariable],
                                    },
                                    async writer =>
                                    {
                                        foreach (var vector in vectors)
                                        {
                                            await WriteVectorLoopAsync(
                                                context,
                                                writer,
                                                compilation,
                                                hostPlatform,
                                                vector,
                                                async writer =>
                                                {
                                                    await writer.WriteExpandAsync(
                                                        new ExpandElementProperties
                                                        {
                                                            Name = "$(MacroName)",
                                                            Attributes =
                                                            {
                                                                { "TargetType", "$(TargetType)" },
                                                                { "TargetName", "$(TargetName)" },
                                                                { "TargetPlatform", "$(TargetPlatform)" },
                                                                { "TargetConfiguration", "$(TargetConfiguration)" },
                                                                { "HostPlatform", hostPlatform },
                                                            },
                                                        });
                                                });
                                        }
                                    });
                            }

                            if (!string.IsNullOrWhiteSpace(compilation.ProjectPath))
                            {
                                foreach (var vector in vectors)
                                {
                                    await WriteVectorLoopAsync(
                                        context,
                                        writer,
                                        compilation,
                                        hostPlatform,
                                        vector,
                                        async writer =>
                                        {
                                            await writer.WriteExpandAsync(
                                                new ExpandElementProperties
                                                {
                                                    Name = "RemoveStalePrecompiledHeaders",
                                                    Attributes =
                                                    {
                                                        { "ProjectPath", compilation.ProjectPath },
                                                        { "TargetName", "$(TargetName)" },
                                                        { "TargetPlatform", "$(TargetPlatform)" },
                                                        { "TargetConfiguration", "$(TargetConfiguration)" },
                                                    },
                                                });
                                            await writer.WriteExpandAsync(
                                                new ExpandElementProperties
                                                {
                                                    Name = "IntegrityCheckExistingDllFilesBeforeCompile",
                                                    Attributes =
                                                    {
                                                        { "TargetPlatform", "$(TargetPlatform)" },
                                                        { "FolderPath", compilation.ProjectPath },
                                                    }
                                                });
                                        });
                                }
                            }

                            foreach (var vector in vectors)
                            {
                                await WriteVectorLoopAsync(
                                    context,
                                    writer,
                                    compilation,
                                    hostPlatform,
                                    vector,
                                    async writer =>
                                    {
                                        // Automatically set the StoreVersion for Android so that it increments over
                                        // time. This is necessary for store submission pretty much everywhere. We have
                                        // to pass this to the Compile tag as well, since the store version is used for
                                        // the debug symbols folder.
                                        await writer.WritePropertyAsync(
                                            new PropertyElementProperties
                                            {
                                                Name = "AdditionalArguments",
                                                Value = "$(AdditionalArguments) -ini:Engine:[/Script/AndroidRuntimeSettings.AndroidRuntimeSettings]:StoreVersion=$(Timestamp)",
                                                If = "'$(TargetPlatform)' == 'Android'"
                                            });
                                        await writer.WriteCompileAsync(
                                            new CompileElementProperties
                                            {
                                                Target = "$(TargetName)",
                                                Platform = "$(TargetPlatform)",
                                                Configuration = "$(TargetConfiguration)",
                                                Tag = $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)_WithIntermediate",
                                                Arguments = vector.Arguments,
                                            });
                                    });
                            }

                            foreach (var vector in vectors)
                            {
                                await WriteVectorLoopAsync(
                                    context,
                                    writer,
                                    compilation,
                                    hostPlatform,
                                    vector,
                                    async writer =>
                                    {
                                        if (!string.IsNullOrWhiteSpace(compilation.StripPath))
                                        {
                                            await writer.WriteExpandAsync(
                                                new ExpandElementProperties
                                                {
                                                    Name = "StripDebugSymbolsForTarget",
                                                    Attributes =
                                                    {
                                                        { "StripPath", compilation.StripPath },
                                                        { "TargetPlatform", "$(TargetPlatform)" },
                                                        { "FilesTag", $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)_WithIntermediate" },
                                                    }
                                                });
                                        }
                                        await writer.WriteExpandAsync(
                                            new ExpandElementProperties
                                            {
                                                Name = "IntegrityCheckNewDllFilesAfterCompile",
                                                Attributes =
                                                {
                                                    { "TargetPlatform", "$(TargetPlatform)" },
                                                    { "FileList", $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)_WithIntermediate" },
                                                }
                                            });
                                        await writer.WritePropertyAsync(
                                            new PropertyElementProperties
                                            {
                                                Name = "BinaryExceptRule",
                                                Value = ""
                                            });
                                        await writer.WritePropertyAsync(
                                            new PropertyElementProperties
                                            {
                                                Name = "BinaryExceptRule",
                                                Value = ".../Intermediate/.../Inc/...",
                                                If = "'$(TargetConfiguration)' != 'Shipping'"
                                            });
                                        await writer.WriteTagAsync(
                                            new TagElementProperties
                                            {
                                                Files = $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)_WithIntermediate",
                                                Except = "$(BinaryExceptRule)",
                                                With = $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                                            });
                                        await writer.WriteTagAsync(
                                            new TagElementProperties
                                            {
                                                Files = $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                                                Filter = "*.target",
                                                With = $"#{vector.TagPrefix}_Receipts_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                                            });
                                        await writer.WriteSanitizeReceiptAsync(
                                            new SanitizeReceiptElementProperties
                                            {
                                                Files = $"#{vector.TagPrefix}_Receipts_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                                            });
                                    });
                            }
                        });
                });
        }

        private static async Task WriteNonUnifiedCompileAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            CompilationContext compilation,
            string hostPlatform,
            IReadOnlyList<CompilationVector> vectors)
        {
            var hash = Hash.Sha256AsHexString(compilation.UniqueName + '|' + hostPlatform, Encoding.UTF8);
            var tempProduces = $"Temp_Produces_{hash}";
            await writer.WritePropertyAsync(
                new PropertyElementProperties
                {
                    Name = tempProduces,
                    Value = "",
                });
            foreach (var vector in vectors)
            {
                await WriteVectorLoopAsync(
                    context,
                    writer,
                    compilation,
                    hostPlatform,
                    vector,
                    async writer =>
                    {
                        await writer.WritePropertyAsync(
                            new PropertyElementProperties
                            {
                                Name = tempProduces,
                                Value = $"$({tempProduces})#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration);#{vector.TagPrefix}_Receipts_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration);",
                            });
                        await compilation.ActOnProductionTag(
                            context,
                            writer,
                            GetCompilationProductionResult(vector.TagPrefix, hostPlatform));

                        await writer.WriteAgentNodeAsync(
                            new AgentNodeElementProperties
                            {
                                AgentName = $"Compile {compilation.UniqueName} $(TargetType) $(TargetName) $(TargetPlatform) $(TargetConfiguration)",
                                AgentType = hostPlatform,
                                AgentStage = string.Empty,
                                NodeName = $"Compile {compilation.UniqueName} $(TargetType) $(TargetName) $(TargetPlatform) $(TargetConfiguration)",
                                Requires = string.Join(';', compilation.Requires),
                                Produces = $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration);#{vector.TagPrefix}_Receipts_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration);",
                            },
                            async writer =>
                            {
                                if (compilation.RunDynamicBeforeCompileMacrosVariable != null)
                                {
                                    await writer.WriteForEachAsync(
                                        new ForEachElementProperties
                                        {
                                            Name = "MacroName",
                                            Values = [compilation.RunDynamicBeforeCompileMacrosVariable],
                                        },
                                        async writer =>
                                        {
                                            await writer.WriteExpandAsync(
                                                new ExpandElementProperties
                                                {
                                                    Name = "$(MacroName)",
                                                    Attributes =
                                                    {
                                                        { "TargetType", "$(TargetType)" },
                                                        { "TargetName", "$(TargetName)" },
                                                        { "TargetPlatform", "$(TargetPlatform)" },
                                                        { "TargetConfiguration", "$(TargetConfiguration)" },
                                                        { "HostPlatform", hostPlatform },
                                                    }
                                                });
                                        });
                                }

                                if (!string.IsNullOrWhiteSpace(compilation.ProjectPath))
                                {
                                    await writer.WriteExpandAsync(
                                        new ExpandElementProperties
                                        {
                                            Name = "RemoveStalePrecompiledHeaders",
                                            Attributes =
                                            {
                                                { "ProjectPath", compilation.ProjectPath },
                                                { "TargetName", "$(TargetName)" },
                                                { "TargetPlatform", "$(TargetPlatform)" },
                                                { "TargetConfiguration", "$(TargetConfiguration)" },
                                            }
                                        });
                                    await writer.WriteExpandAsync(
                                        new ExpandElementProperties
                                        {
                                            Name = "IntegrityCheckExistingDllFilesBeforeCompile",
                                            Attributes =
                                            {
                                                { "TargetPlatform", "$(TargetPlatform)" },
                                                { "FolderPath", compilation.ProjectPath },
                                            }
                                        });
                                }

                                await writer.WriteCompileAsync(
                                    new CompileElementProperties
                                    {
                                        Target = "$(TargetName)",
                                        Platform = "$(TargetPlatform)",
                                        Configuration = "$(TargetConfiguration)",
                                        Tag = $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)_WithIntermediate",
                                        Arguments = vector.Arguments,
                                    });

                                if (!string.IsNullOrWhiteSpace(compilation.StripPath))
                                {
                                    await writer.WriteExpandAsync(
                                        new ExpandElementProperties
                                        {
                                            Name = "StripDebugSymbolsForTarget",
                                            Attributes =
                                            {
                                                { "StripPath", compilation.StripPath },
                                                { "TargetPlatform", "$(TargetPlatform)" },
                                                { "FilesTag", $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)_WithIntermediate" },
                                            }
                                        });
                                }
                                await writer.WriteExpandAsync(
                                    new ExpandElementProperties
                                    {
                                        Name = "IntegrityCheckNewDllFilesAfterCompile",
                                        Attributes =
                                        {
                                            { "TargetPlatform", "$(TargetPlatform)" },
                                            { "FileList", $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)_WithIntermediate" },
                                        }
                                    });
                                await writer.WritePropertyAsync(
                                    new PropertyElementProperties
                                    {
                                        Name = "BinaryExceptRule",
                                        Value = ""
                                    });
                                await writer.WritePropertyAsync(
                                    new PropertyElementProperties
                                    {
                                        Name = "BinaryExceptRule",
                                        Value = ".../Intermediate/.../Inc/...",
                                        If = "'$(TargetConfiguration)' != 'Shipping'"
                                    });
                                await writer.WriteTagAsync(
                                    new TagElementProperties
                                    {
                                        Files = $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)_WithIntermediate",
                                        Except = "$(BinaryExceptRule)",
                                        With = $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                                    });
                                await writer.WriteTagAsync(
                                    new TagElementProperties
                                    {
                                        Files = $"#{vector.TagPrefix}_Binaries_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                                        Filter = "*.target",
                                        With = $"#{vector.TagPrefix}_Receipts_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                                    });
                                await writer.WriteSanitizeReceiptAsync(
                                    new SanitizeReceiptElementProperties
                                    {
                                        Files = $"#{vector.TagPrefix}_Receipts_$(TargetType)_$(TargetName)_$(TargetPlatform)_$(TargetConfiguration)",
                                    });
                            });
                        if (compilation.BuildTasksVariable != null)
                        {
                            await writer.WritePropertyAsync(
                                new PropertyElementProperties
                                {
                                    Name = compilation.BuildTasksVariable,
                                    Value = $"$({compilation.BuildTasksVariable})Compile {compilation.UniqueName} $(TargetType) $(TargetName) $(TargetPlatform) $(TargetConfiguration);"
                                });
                        }
                    });
            }
        }

        public async Task WriteBuildGraphNodesToCompileAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            CompilationContext compilation,
            IReadOnlyList<CompilationVector> vectors)
        {
            // Write unified build nodes. Unified builds leverage UBA to distribute the compilation work, so we need
            // fewer build jobs on CI/CD and consequently, fewer workspace checkouts. If you have UBA set up, this is
            // much faster than a non-unified build.
            await writer.WriteDoAsync(
                new DoElementProperties
                {
                    If = "'$(UseUnifiedBuild)' == 'true'"
                },
                async writer =>
                {
                    await writer.WriteCommentAsync("Win64 Unified");
                    await WriteUnifiedCompileAsync(
                        context,
                        writer,
                        compilation,
                        "Win64",
                        vectors);

                    await writer.WriteCommentAsync("Mac Unified");
                    await WriteUnifiedCompileAsync(
                        context,
                        writer,
                        compilation,
                        "Mac",
                        vectors);
                });

            // Write non-unified build nodes. This is the legacy method of distributing build work. This is slower because
            // each build job needs to set up the workspace, checkout files, and can't leverage any common intermediate files
            // between e.g.Windows editor and game targets. Eventually we'll phase this out completely, but for now it's still
            // used for Unreal Engine 5.5 and earlier where UBA isn't as stable.
            await writer.WriteDoAsync(
                new DoElementProperties
                {
                    If = "'$(UseUnifiedBuild)' == 'false'"
                },
                async writer =>
                {
                    await writer.WriteCommentAsync("Win64 Non-Unified");
                    await WriteNonUnifiedCompileAsync(
                        context,
                        writer,
                        compilation,
                        "Win64",
                        vectors);

                    await writer.WriteCommentAsync("Mac Non-Unified");
                    await WriteNonUnifiedCompileAsync(
                        context,
                        writer,
                        compilation,
                        "Mac",
                        vectors);
                });
        }
    }
}
