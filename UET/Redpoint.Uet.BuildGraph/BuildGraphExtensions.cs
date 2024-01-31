namespace Redpoint.Uet.BuildGraph
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Text;
    using System.Text.Json;
    using System.Xml;
    using Redpoint.Concurrency;

    public static class BuildGraphExtensions
    {
        public static async Task WriteAgentNodeAsync(
            this XmlWriter writer,
            AgentNodeElementProperties props,
            Func<XmlWriter, Task> writeChildren)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);
            ArgumentNullException.ThrowIfNull(writeChildren);

            await writer.WriteStartElementAsync(null, "Agent", null).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, $"{props.NodeName} ({props.AgentStage})").ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Type", null, props.AgentType).ConfigureAwait(false);

            await writer.WriteStartElementAsync(null, "Node", null).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Name", null, props.NodeName).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(props.Requires))
            {
                await writer.WriteAttributeStringAsync(null, "Requires", null, props.Requires).ConfigureAwait(false);
            }
            if (!string.IsNullOrWhiteSpace(props.Produces))
            {
                await writer.WriteAttributeStringAsync(null, "Produces", null, props.Produces).ConfigureAwait(false);
            }
            await writeChildren(writer).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);

            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        public static async Task WritePropertyAsync(
            this XmlWriter writer,
            PropertyElementProperties props)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);

            await writer.WriteStartElementAsync(null, "Property", null).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, props.Name).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Value", null, props.Value).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        public static async Task WriteExpandAsync(
            this XmlWriter writer,
            ExpandElementProperties props)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);

            await writer.WriteStartElementAsync(null, "Expand", null).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Name", null, props.Name).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            foreach (var kv in props.Attributes)
            {
                await writer.WriteAttributeStringAsync(null, kv.Key, null, kv.Value).ConfigureAwait(false);
            }
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        public static async Task WriteCompileAsync(
            this XmlWriter writer,
            CompileElementProperties props)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);

            await writer.WriteStartElementAsync(null, "Compile", null).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Target", null, props.Target).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Platform", null, props.Platform).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Configuration", null, props.Configuration).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Tag", null, props.Tag).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Arguments", null, string.Join(" ", props.Arguments)).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        public static async Task WriteCopyAsync(
            this XmlWriter writer,
            CopyElementProperties props)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);

            await writer.WriteStartElementAsync(null, "Copy", null).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Files", null, props.Files).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "From", null, props.From).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "To", null, props.To).ConfigureAwait(false);
            if (props.Tag != null)
            {
                await writer.WriteAttributeStringAsync(null, "Tag", null, props.Tag).ConfigureAwait(false);
            }
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        public static async Task WriteDeleteAsync(
            this XmlWriter writer,
            DeleteElementProperties props)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);

            await writer.WriteStartElementAsync(null, "Delete", null).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Files", null, props.Files).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        public static async Task WriteSpawnAsync(
            this XmlWriter writer,
            SpawnElementProperties props)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);

            await writer.WriteStartElementAsync(null, "Spawn", null).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Exe", null, props.Exe).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Arguments", null, string.Join(" ", props.Arguments)).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        public static async Task WriteTagAsync(
            this XmlWriter writer,
            TagElementProperties props)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);

            await writer.WriteStartElementAsync(null, "Tag", null).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "BaseDir", null, props.BaseDir).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Files", null, props.Files).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "With", null, props.With).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        public static async Task WriteDynamicNodeAppendAsync(
            this XmlWriter writer,
            DynamicNodeAppendElementProperties props)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);

            await writer.WriteStartElementAsync(null, "Property", null).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, "DynamicNodes").ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Value", null, $"$(DynamicNodes){props.NodeName};").ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);

            if (props.MustPassForLaterDeployment)
            {
                await writer.WriteStartElementAsync(null, "Property", null).ConfigureAwait(false);
                if (props.If != null)
                {
                    await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
                }
                await writer.WriteAttributeStringAsync(null, "Name", null, "DynamicPreDeploymentNodes").ConfigureAwait(false);
                await writer.WriteAttributeStringAsync(null, "Value", null, $"$(DynamicPreDeploymentNodes){props.NodeName};").ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }
        }

        public static async Task WriteDynamicOutputFileAppendAsync(
            this XmlWriter writer,
            DynamicOutputFileAppendElementProperties props)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);

            await writer.WriteStartElementAsync(null, "Property", null).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, "DynamicOutputFiles").ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Value", null, $"$(DynamicOutputFiles){props.Tag};").ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        public static async Task WriteDynamicReentrantSpawnAsync<T, TDistribution, TConfig>(
            this XmlWriter writer,
            T instance,
            IBuildGraphEmitContext context,
            string temporaryPathNamePrefix,
            TConfig config,
            Dictionary<string, string> runtimeSettings) where T : IDynamicReentrantExecutor<TDistribution, TConfig>
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(runtimeSettings);

            var globalArgsProvider = context.Services.GetService<IGlobalArgsProvider>();

            string json;
            using (var stream = new MemoryStream())
            {
                await using (new Utf8JsonWriter(stream).AsAsyncDisposable(out var jsonWriter).ConfigureAwait(false))
                {
                    instance.DynamicSettings.Serialize(jsonWriter, config!);
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

            await writer.WriteStartElementAsync(null, "WriteTextFile", null).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "File", null, emitPath).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Text", null, json).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);

            await writer.WriteSpawnAsync(
                new SpawnElementProperties
                {
                    Exe = "$(UETPath)",
                    Arguments = args.ToArray()
                }).ConfigureAwait(false);
        }

        public static async Task WriteMacroAsync(
            this XmlWriter writer,
            MacroElementProperties props,
            Func<XmlWriter, Task> writeChildren)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(props);
            ArgumentNullException.ThrowIfNull(writeChildren);

            await writer.WriteStartElementAsync(null, "Macro", null).ConfigureAwait(false);
            if (props.If != null)
            {
                await writer.WriteAttributeStringAsync(null, "If", null, props.If).ConfigureAwait(false);
            }
            await writer.WriteAttributeStringAsync(null, "Name", null, props.Name).ConfigureAwait(false);
            await writer.WriteAttributeStringAsync(null, "Arguments", null, string.Join(";", props.Arguments)).ConfigureAwait(false);
            await writeChildren(writer).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
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

        public required IReadOnlyCollection<string> Arguments { get; set; }
    }

    public record class MacroElementProperties : ElementProperties
    {
        public required string Name { get; set; }

        public required IReadOnlyCollection<string> Arguments { get; set; }
    }
}