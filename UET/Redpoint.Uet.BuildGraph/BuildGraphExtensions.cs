namespace Redpoint.Uet.BuildGraph
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Xml;

    public static class BuildGraphExtensions
    {
        public static async Task WriteAgentAsync(
            this XmlWriter writer,
            AgentElementProperties props,
            Func<XmlWriter, Task> writeChildren)
        {
            await writer.WriteStartElementAsync(null, "Agent", null);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, props.Name);
            await writer.WriteAttributeStringAsync(null, "Type", null, props.Type);
            await writeChildren(writer);
            await writer.WriteEndElementAsync();
        }

        public static async Task WritePropertyAsync(
            this XmlWriter writer,
            PropertyElementProperties props)
        {
            await writer.WriteStartElementAsync(null, "Property", null);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, props.Name);
            await writer.WriteAttributeStringAsync(null, "Value", null, props.Value);
            await writer.WriteEndElementAsync();
        }

        public static async Task WriteExpandAsync(
            this XmlWriter writer,
            ExpandElementProperties props)
        {
            await writer.WriteStartElementAsync(null, "Expand", null);
            await writer.WriteAttributeStringAsync(null, "Name", null, props.Name);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            foreach (var kv in props.Attributes)
            {
                await writer.WriteAttributeStringAsync(null, kv.Key, null, kv.Value);
            }
            await writer.WriteEndElementAsync();
        }

        public static async Task WriteNodeAsync(
            this XmlWriter writer,
            NodeElementProperties props,
            Func<XmlWriter, Task> writeChildren)
        {
            await writer.WriteStartElementAsync(null, "Node", null);
            await writer.WriteAttributeStringAsync(null, "Name", null, props.Name);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            if (!string.IsNullOrWhiteSpace(props.Requires))
            {
                await writer.WriteAttributeStringAsync(null, "Requires", null, props.Requires);
            }
            if (!string.IsNullOrWhiteSpace(props.Produces))
            {
                await writer.WriteAttributeStringAsync(null, "Produces", null, props.Produces);
            }
            await writeChildren(writer);
            await writer.WriteEndElementAsync();
        }

        public static async Task WriteCompileAsync(
            this XmlWriter writer,
            CompileElementProperties props)
        {
            await writer.WriteStartElementAsync(null, "Compile", null);
            await writer.WriteAttributeStringAsync(null, "Target", null, props.Target);
            await writer.WriteAttributeStringAsync(null, "Platform", null, props.Platform);
            await writer.WriteAttributeStringAsync(null, "Configuration", null, props.Configuration);
            await writer.WriteAttributeStringAsync(null, "Tag", null, props.Tag);
            await writer.WriteAttributeStringAsync(null, "Arguments", null, string.Join(" ", props.Arguments));
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteEndElementAsync();
        }

        public static async Task WriteCopyAsync(
            this XmlWriter writer,
            CopyElementProperties props)
        {
            await writer.WriteStartElementAsync(null, "Copy", null);
            await writer.WriteAttributeStringAsync(null, "Files", null, props.Files);
            await writer.WriteAttributeStringAsync(null, "From", null, props.From);
            await writer.WriteAttributeStringAsync(null, "To", null, props.To);
            if (props.Tag != null)
            {
                await writer.WriteAttributeStringAsync(null, "Tag", null, props.Tag);
            }
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteEndElementAsync();
        }

        public static async Task WriteDeleteAsync(
            this XmlWriter writer,
            DeleteElementProperties props)
        {
            await writer.WriteStartElementAsync(null, "Delete", null);
            await writer.WriteAttributeStringAsync(null, "Files", null, props.Files);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteEndElementAsync();
        }

        public static async Task WriteSpawnAsync(
            this XmlWriter writer,
            SpawnElementProperties props)
        {
            await writer.WriteStartElementAsync(null, "Spawn", null);
            await writer.WriteAttributeStringAsync(null, "Exe", null, props.Exe);
            await writer.WriteAttributeStringAsync(null, "Arguments", null, string.Join(" ", props.Arguments));
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteEndElementAsync();
        }

        public static async Task WriteTagAsync(
            this XmlWriter writer,
            TagElementProperties props)
        {
            await writer.WriteStartElementAsync(null, "Tag", null);
            await writer.WriteAttributeStringAsync(null, "BaseDir", null, props.BaseDir);
            await writer.WriteAttributeStringAsync(null, "Files", null, props.Files);
            await writer.WriteAttributeStringAsync(null, "With", null, props.With);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteEndElementAsync();
        }

        public static async Task WriteDynamicNodeAppendAsync(
            this XmlWriter writer,
            DynamicNodeAppendElementProperties props)
        {
            await writer.WriteStartElementAsync(null, "Property", null);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, "DynamicNodes");
            await writer.WriteAttributeStringAsync(null, "Value", null, $"$(DynamicNodes){props.NodeName};");
            await writer.WriteEndElementAsync();

            if (props.MustPassForLaterDeployment)
            {
                await writer.WriteStartElementAsync(null, "Property", null);
                if (props.If != null)
                {
                    await writer.WriteAttributeStringAsync(null, "If", null, props.If);
                }
                await writer.WriteAttributeStringAsync(null, "Name", null, "DynamicPreDeploymentNodes");
                await writer.WriteAttributeStringAsync(null, "Value", null, $"$(DynamicPreDeploymentNodes){props.NodeName};");
                await writer.WriteEndElementAsync();
            }
        }

        public static async Task WriteDynamicOutputFileAppendAsync(
            this XmlWriter writer,
            DynamicOutputFileAppendElementProperties props)
        {
            await writer.WriteStartElementAsync(null, "Property", null);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, "DynamicOutputFiles");
            await writer.WriteAttributeStringAsync(null, "Value", null, $"$(DynamicOutputFiles){props.Tag};");
            await writer.WriteEndElementAsync();
        }

        public static async Task WriteDynamicReentrantSpawnAsync<T, TDistribution, TConfig>(
            this XmlWriter writer,
            T instance,
            IBuildGraphEmitContext context,
            string temporaryPathNamePrefix,
            TConfig config,
            Dictionary<string, string> runtimeSettings) where T : IDynamicReentrantExecutor<TDistribution, TConfig>
        {
            var globalArgsProvider = context.Services.GetService<IGlobalArgsProvider>();

            string json;
            using (var stream = new MemoryStream())
            {
                await using (var jsonWriter = new Utf8JsonWriter(stream))
                {
                    instance.SerializeDynamicSettings(jsonWriter, config, new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        Converters =
                        {
                            new JsonStringEnumConverter(),
                        }
                    });
                }
                var buffer = new byte[stream.Position];
                stream.Seek(0, SeekOrigin.Begin);
                stream.Read(buffer);
                json = Encoding.UTF8.GetString(buffer);
            }

            var emitPath = $"$(TempPath)/{temporaryPathNamePrefix}.{Guid.NewGuid()}.json";

            var args = new List<string>();
            args.AddRange(globalArgsProvider?.GlobalArgsArray ?? Array.Empty<string>());
            args.AddRange(new[]
            {
                "internal",
                "run-dynamic-reentrant-task",
                "--distribution-type",
                typeof(TDistribution) switch
                {
                    var t when t == typeof(BuildConfigPluginDistribution) => "plugin",
                    var t when t == typeof(BuildConfigProjectDistribution) => "project",
                    _ => throw new InvalidOperationException("Unsupported distribution type"),
                },
                "--reentrant-executor-category",
                instance switch
                {
                    IPrepareProvider => "prepare",
                    ITestProvider => "test",
                    IDeploymentProvider => "deployment",
                    var x => throw new InvalidOperationException($"Unsupported executor type on {x.GetType().FullName}"),
                },
                "--reentrant-executor",
                instance.Type,
                "--task-json-path",
                emitPath,
            });
            foreach (var kv in runtimeSettings)
            {
                args.AddRange(new[]
                {
                    "--runtime-setting",
                    $@"{kv.Key}=""{kv.Value}"""
                });
            }

            await writer.WriteStartElementAsync(null, "WriteTextFile", null);
            await writer.WriteAttributeStringAsync(null, "File", null, emitPath);
            await writer.WriteAttributeStringAsync(null, "Text", null, json);
            await writer.WriteEndElementAsync();

            await writer.WriteSpawnAsync(
                new SpawnElementProperties
                {
                    Exe = "$(UETPath)",
                    Arguments = args.ToArray()
                });
        }

        public static async Task WriteMacroAsync(
            this XmlWriter writer,
            MacroElementProperties props,
            Func<XmlWriter, Task> writeChildren)
        {
            await writer.WriteStartElementAsync(null, "Macro", null);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, props.Name);
            await writer.WriteAttributeStringAsync(null, "Arguments", null, string.Join(";", props.Arguments));
            await writeChildren(writer);
            await writer.WriteEndElementAsync();
        }
    }

    public record class DynamicNodeAppendElementProperties : ElementProperties
    {
        public required string NodeName { get; set; }
        public bool MustPassForLaterDeployment { get; set; }
    }

    public record class DynamicOutputFileAppendElementProperties : ElementProperties
    {
        public required string Tag { get; set; }
    }

    public record class DeleteElementProperties : ElementProperties
    {
        public required string Files { get; set; }
    }

    public record class TagElementProperties : ElementProperties
    {
        public required string BaseDir { get; set; }

        public required string Files { get; set; }

        public required string With { get; set; }
    }

    public record class SpawnElementProperties : ElementProperties
    {
        public required string Exe { get; set; }

        public required string[] Arguments { get; set; }
    }

    public record class MacroElementProperties : ElementProperties
    {
        public required string Name { get; set; }

        public required string[] Arguments { get; set; }
    }
}