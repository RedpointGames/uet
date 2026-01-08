namespace Redpoint.YamlToJson
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using YamlDotNet.Core;
    using YamlDotNet.Core.Tokens;
    using YamlDotNet.RepresentationModel;

    public static class YamlToJsonConverter
    {
        public static string Convert(string yaml, JsonWriterOptions jsonWriterOptions = default)
        {
            using var yamlInputStream = new MemoryStream();
            using var jsonOutputStream = new MemoryStream();

            using (var writer = new StreamWriter(yamlInputStream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(yaml);
            }

            Convert(yamlInputStream, jsonOutputStream, jsonWriterOptions);

            jsonOutputStream.Seek(0, SeekOrigin.Begin);

            using (var reader = new StreamReader(jsonOutputStream))
            {
                return reader.ReadToEnd();
            }
        }

        public static void Convert(Stream yamlInputStream, Stream jsonOutputStream, JsonWriterOptions jsonWriterOptions = default)
        {
            YamlStream yamlStream;
            using (var reader = new StreamReader(yamlInputStream))
            {
                yamlStream = new YamlStream();
                yamlStream.Load(reader);
            }

            using var jsonWriter = new Utf8JsonWriter(jsonOutputStream, jsonWriterOptions);

            if (yamlStream.Documents.Count == 1)
            {
                VisitNode(jsonWriter, yamlStream.Documents[0].RootNode);
            }
            else
            {
                jsonWriter.WriteStartArray();
                foreach (var document in yamlStream.Documents)
                {
                    VisitNode(jsonWriter, document.RootNode);
                }
                jsonWriter.WriteEndArray();
            }
        }

        private static void VisitNode(Utf8JsonWriter writer, YamlNode node)
        {
            switch (node)
            {
                case YamlScalarNode scalarNode:
                    VisitScalarNode(writer, scalarNode);
                    break;
                case YamlSequenceNode sequenceNode:
                    VisitSequenceNode(writer, sequenceNode);
                    break;
                case YamlMappingNode mappingNode:
                    VisitMappingNode(writer, mappingNode);
                    break;
                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        private static readonly Regex _booleanTruePattern = new Regex("^(true|y|yes|on)$");
        private static readonly Regex _booleanFalsePattern = new Regex("^(false|n|no|off)$");

        private static void VisitScalarNode(Utf8JsonWriter writer, YamlScalarNode scalarNode)
        {
            bool forceImplicitPlain = false;
            if (scalarNode.Style == ScalarStyle.Plain && scalarNode.Tag.IsEmpty &&
                scalarNode.Value != null)
            {
                forceImplicitPlain = scalarNode.Value.Length switch
                {
                    // we have an implicit null value without a tag stating it, fake it out
                    0 => true,
                    1 => scalarNode.Value == "~",
                    4 => scalarNode.Value == "null" || scalarNode.Value == "Null" || scalarNode.Value == "NULL",
                    // for backwards compatability we won't be setting the Value property to null
                    _ => false
                };
            }
            if (forceImplicitPlain && scalarNode.Style == ScalarStyle.Plain && string.IsNullOrEmpty(scalarNode.Value))
            {
                writer.WriteNullValue();
                return;
            }
            if (scalarNode.Tag.IsEmpty && scalarNode.Value == null && (scalarNode.Style == ScalarStyle.Plain || scalarNode.Style == ScalarStyle.Any))
            {
                writer.WriteNullValue();
                return;
            }
            if (scalarNode.Value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // @todo: Respect tags.

            if (scalarNode.Style == ScalarStyle.SingleQuoted ||
                scalarNode.Style == ScalarStyle.DoubleQuoted ||
                scalarNode.Style == ScalarStyle.Literal ||
                scalarNode.Style == ScalarStyle.Folded)
            {
                // All of these styles are explicitly string values.
                writer.WriteStringValue(scalarNode.Value);
                return;
            }

            if (_booleanTruePattern.IsMatch(scalarNode.Value))
            {
                writer.WriteBooleanValue(true);
                return;
            }
            if (_booleanFalsePattern.IsMatch(scalarNode.Value))
            {
                writer.WriteBooleanValue(false);
                return;
            }

            if (long.TryParse(scalarNode.Value, out var longValue))
            {
                writer.WriteNumberValue(longValue);
                return;
            }
            if (ulong.TryParse(scalarNode.Value, out var ulongValue))
            {
                writer.WriteNumberValue(ulongValue);
                return;
            }
            if (double.TryParse(scalarNode.Value, out var doubleValue))
            {
                writer.WriteNumberValue(doubleValue);
                return;
            }

            writer.WriteStringValue(scalarNode.Value);
            return;
        }

        private static void VisitSequenceNode(Utf8JsonWriter writer, YamlSequenceNode sequenceNode)
        {
            writer.WriteStartArray();

            foreach (var node in sequenceNode.Children)
            {
                VisitNode(writer, node);
            }

            writer.WriteEndArray();
        }

        private static void VisitMappingNode(Utf8JsonWriter writer, YamlMappingNode mappingNode)
        {
            writer.WriteStartObject();

            foreach (var kv in mappingNode.Children)
            {
                if (kv.Key is YamlScalarNode keyScalarNode &&
                    !string.IsNullOrWhiteSpace(keyScalarNode.Value))
                {
                    writer.WritePropertyName(keyScalarNode.Value);

                    VisitNode(writer, kv.Value);
                }
            }

            writer.WriteEndObject();
        }
    }
}
