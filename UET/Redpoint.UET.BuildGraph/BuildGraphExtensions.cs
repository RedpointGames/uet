namespace Redpoint.UET.BuildGraph
{
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
    }

    public record class DynamicNodeAppendElementProperties : ElementProperties
    {
        public required string NodeName { get; set; }
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
}