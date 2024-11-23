namespace UET.Commands.Internal.GenerateJsonSchema
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Reflection;
    using System.Text;
    using System.Text.Json.Serialization.Metadata;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml;
    using System.IO;
    using Microsoft.Extensions.Logging;

    internal sealed class DefaultJsonSchemaGenerator : IJsonSchemaGenerator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DefaultJsonSchemaGenerator> _logger;
        private readonly List<XmlDocument> _descriptionDocuments;

        public DefaultJsonSchemaGenerator(
            ILogger<DefaultJsonSchemaGenerator> logger,
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            _descriptionDocuments = Assembly.GetExecutingAssembly()
                .GetManifestResourceNames()
                .Where(x => x.EndsWith(".xml", StringComparison.Ordinal))
                .Select(x =>
                {
                    var doc = new XmlDocument();
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(x))
                    {
                        doc.Load(stream!);
                    }
                    return doc;
                })
                .ToList();
        }

        public ValueTask GenerateAsync(Stream outputStream)
        {
            using (var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions { Indented = true }))
            {
                GenerateSchemaForObject(
                    writer,
                    BuildConfigSourceGenerationContext.Default.BuildConfig,
                    BuildConfigSourceGenerationContext.Default);
            }
            return ValueTask.CompletedTask;
        }

        private static string ProcessSummary(XmlNode summary)
        {
            var sb = new StringBuilder();
            foreach (var child in summary.ChildNodes)
            {
                if (child is XmlElement el)
                {
                    switch (el.Name)
                    {
                        case "see":
                            sb.AppendLine(el.GetAttribute("cref").Split(".").Last());
                            break;
                    }
                }
                else if (child is XmlText text)
                {
                    sb.Append(text.InnerText);
                }
            }
            var lines = sb.ToString().Replace("\r", "", StringComparison.Ordinal).Replace("\t", "    ", StringComparison.Ordinal).Split('\n');
            var nsb = new StringBuilder();
            var hasContent = false;
            var indent = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                if (!hasContent && string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }
                if (!hasContent)
                {
                    hasContent = true;
                    indent = lines[i].Length - lines[i].TrimStart().Length;
                }
                if (lines[i].Length > indent)
                {
                    nsb.AppendLine(lines[i][indent..].TrimEnd());
                }
                else
                {
                    nsb.AppendLine();
                }
            }
            return nsb.ToString().Trim().Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private void GenerateSchemaForObject(Utf8JsonWriter writer, JsonTypeInfo jsonTypeInfo, JsonSerializerContext jsonTypeInfoResolver)
        {
            var properties = jsonTypeInfo.Properties;
            if (jsonTypeInfo.Type.IsConstructedGenericType &&
                jsonTypeInfo.Type.GetGenericTypeDefinition() == typeof(BuildConfigDynamic<,>))
            {
                // Exclude dynamically driven properties for BuildConfigDynamic.
                properties = properties.Where(x => x.Name != "DynamicSettings" && x.Name != "Type").ToList();
            }

            writer.WriteStartObject();
            writer.WriteString("type", "object");
            {
                var fullName = jsonTypeInfo.Type.FullName;
                if (jsonTypeInfo.Type.IsConstructedGenericType)
                {
                    fullName = jsonTypeInfo.Type.GetGenericTypeDefinition().FullName;
                }
                var summary = _descriptionDocuments
                    .SelectMany(x => x.SelectNodes($@"/doc/members/member[@name=""T:{fullName}""]/summary")!.OfType<XmlNode>())
                    .FirstOrDefault();
                if (summary != null)
                {
                    writer.WriteString("description", ProcessSummary(summary));
                }
                else
                {
                    _logger.LogWarning($"{fullName} is undocumented. This might be because the C# property name does not match the JSON property name. Due to trimming, the C# name must exactly match the name used in JSON.");
                }
            }
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            if (jsonTypeInfo.Type == typeof(BuildConfig))
            {
                writer.WritePropertyName("$schema");
                writer.WriteBooleanValue(true);
            }
            GeneratePropertiesForObject(
                writer,
                jsonTypeInfo,
                jsonTypeInfoResolver,
                properties);

            // Write out all the possible values of Type.
            if (jsonTypeInfo.Type.IsConstructedGenericType &&
                jsonTypeInfo.Type.GetGenericTypeDefinition() == typeof(BuildConfigDynamic<,>))
            {
                var dynamicProviderType = typeof(IDynamicProvider<,>)
                    .MakeGenericType(jsonTypeInfo.Type.GetGenericArguments());
                var types = _serviceProvider
                    .GetServices(dynamicProviderType)
                    .OfType<IDynamicProviderRegistration>()
                    .Select(x => x.Type)
                    .ToArray();
                writer.WritePropertyName("Type");
                writer.WriteStartArray();
                foreach (var type in types)
                {
                    writer.WriteStringValue(type);
                }
                writer.WriteStringValue(BuildConfigConstants.Predefined);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
            if (properties.Any(x => x.IsRequired))
            {
                writer.WritePropertyName("required");
                writer.WriteStartArray();
                foreach (var property in properties.Where(x => x.IsRequired))
                {
                    writer.WriteStringValue(property.Name);
                }
                writer.WriteEndArray();
            }

            // If this is the BuildConfig type itself, we need to branch out into
            // the BuildConfigEngine, BuildConfigPlugin or BuildConfigProject
            // depending on what Type is set to.
            if (jsonTypeInfo.Type == typeof(BuildConfig))
            {
                var basePropertyNames = properties.Select(x => x.Name).ToHashSet();
                var mappings = new Dictionary<BuildConfigType, JsonTypeInfo>
                    {
                        {
                            BuildConfigType.Project,
                            BuildConfigSourceGenerationContext.Default.BuildConfigProject
                        },
                        {
                            BuildConfigType.Plugin,
                            BuildConfigSourceGenerationContext.Default.BuildConfigPlugin
                        },
                        {
                            BuildConfigType.Engine,
                            BuildConfigSourceGenerationContext.Default.BuildConfigEngine
                        },
                    };
                writer.WritePropertyName("allOf");
                writer.WriteStartArray();
                foreach (var mapping in mappings)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("if");
                    writer.WriteStartObject();
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WritePropertyName(nameof(BuildConfig.Type));
                    writer.WriteStartObject();
                    writer.WriteString("const", Enum.GetName(mapping.Key));
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WritePropertyName("then");
                    writer.WriteStartObject();
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    GeneratePropertiesForObject(
                        writer,
                        mapping.Value,
                        BuildConfigSourceGenerationContext.Default,
                        mapping.Value.Properties
                            .Where(x => !basePropertyNames.Contains(x.Name))
                            .ToList());
                    writer.WriteEndObject();
                    if (mapping.Value.Properties
                        .Where(x => !basePropertyNames.Contains(x.Name))
                        .Any(x => x.IsRequired))
                    {
                        writer.WritePropertyName("required");
                        writer.WriteStartArray();
                        foreach (var property in mapping.Value.Properties
                            .Where(x => !basePropertyNames.Contains(x.Name))
                            .Where(x => x.IsRequired))
                        {
                            writer.WriteStringValue(property.Name);
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            else if (jsonTypeInfo.Type.IsConstructedGenericType &&
                jsonTypeInfo.Type.GetGenericTypeDefinition() == typeof(BuildConfigDynamic<,>))
            {
                writer.WritePropertyName("allOf");
                writer.WriteStartArray();
                var dynamicProviderType = typeof(IDynamicProvider<,>)
                    .MakeGenericType(jsonTypeInfo.Type.GetGenericArguments());
                var providers = _serviceProvider
                    .GetServices(dynamicProviderType)
                    .OfType<IDynamicProviderRegistration>()
                    .OfType<IDynamicProvider>()
                    .ToList();
                foreach (var type in providers)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("if");
                    writer.WriteStartObject();
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Type");
                    writer.WriteStartObject();
                    writer.WriteString("const", ((IDynamicProviderRegistration)type).Type);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WritePropertyName("then");
                    writer.WriteStartObject();
                    writer.WritePropertyName("required");
                    writer.WriteStartArray();
                    writer.WriteStringValue(((IDynamicProviderRegistration)type).Type);
                    writer.WriteEndArray();
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WriteBoolean(BuildConfigConstants.Predefined, false);
                    foreach (var otherProvider in providers.Where(x => ((IDynamicProviderRegistration)x).Type != ((IDynamicProviderRegistration)type).Type))
                    {
                        writer.WriteBoolean(((IDynamicProviderRegistration)otherProvider).Type, false);
                    }
                    writer.WritePropertyName(((IDynamicProviderRegistration)type).Type);
                    writer.WriteStartObject();
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    GeneratePropertiesForObject(
                        writer,
                        type.DynamicSettings.JsonTypeInfo,
                        type.DynamicSettings.JsonSerializerContext,
                        type.DynamicSettings.JsonTypeInfo.Properties);
                    writer.WriteEndObject();
                    if (type.DynamicSettings.JsonTypeInfo.Properties.Any(x => x.IsRequired))
                    {
                        writer.WritePropertyName("required");
                        writer.WriteStartArray();
                        foreach (var property in type.DynamicSettings.JsonTypeInfo.Properties.Where(x => x.IsRequired))
                        {
                            writer.WriteStringValue(property.Name);
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("if");
                    writer.WriteStartObject();
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    writer.WritePropertyName("Type");
                    writer.WriteStartObject();
                    writer.WriteString("const", BuildConfigConstants.Predefined);
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WritePropertyName("then");
                    writer.WriteStartObject();
                    writer.WritePropertyName("required");
                    writer.WriteStartArray();
                    writer.WriteStringValue(BuildConfigConstants.Predefined);
                    writer.WriteEndArray();
                    writer.WritePropertyName("properties");
                    writer.WriteStartObject();
                    foreach (var otherProvider in providers)
                    {
                        writer.WriteBoolean(((IDynamicProviderRegistration)otherProvider).Type, false);
                    }
                    writer.WritePropertyName(BuildConfigConstants.Predefined);
                    writer.WriteStartObject();
                    writer.WriteString("type", "string");
                    writer.WriteString("description", "The predefined name defined earlier in configuration.");
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            else
            {
                // @note: This doesn't work for objects that are extended
                // with the allOf/if pattern.
                writer.WriteBoolean("additionalProperties", false);
            }

            writer.WriteEndObject();
        }

        private void GeneratePropertiesForObject(
            Utf8JsonWriter writer,
            JsonTypeInfo jsonTypeInfo,
            JsonSerializerContext jsonTypeInfoResolver,
            IList<JsonPropertyInfo> properties)
        {
            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string) && property.Name == "UETVersion")
                {
                    // We don't write this property, because it's controlled by root.json.
                    continue;
                }

                writer.WritePropertyName(property.Name);
                GenerateSchemaForType(
                    writer,
                    property.PropertyType,
                    jsonTypeInfoResolver,
                    () =>
                    {
                        EmitSchemaForXmlComments(writer, jsonTypeInfo, property);
                    });
            }
        }

        private void EmitSchemaForXmlComments(Utf8JsonWriter writer, JsonTypeInfo jsonTypeInfo, JsonPropertyInfo property)
        {
            var fullName = jsonTypeInfo.Type.FullName;
            if (jsonTypeInfo.Type.IsConstructedGenericType)
            {
                fullName = jsonTypeInfo.Type.GetGenericTypeDefinition().FullName;
            }
            var summary = _descriptionDocuments
                .SelectMany(x => x.SelectNodes($@"/doc/members/member[@name=""P:{fullName}.{property.Name}""]/summary")!.OfType<XmlNode>())
                .FirstOrDefault();
            if (summary != null)
            {
                writer.WriteString("description", ProcessSummary(summary));
            }
            else
            {
                _logger.LogWarning($"{fullName}.{property.Name} is undocumented. This might be because the C# property name does not match the JSON property name. Due to trimming, the C# name must exactly match the name used in JSON.");
            }
        }

        private void GenerateSchemaForType(
            Utf8JsonWriter writer,
            Type type,
            JsonSerializerContext jsonTypeInfoResolver,
            Action? writeMetadata)
        {
            switch (type)
            {
                case var t when t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>):
                    GenerateSchemaForType(writer, type.GetGenericArguments()[0], jsonTypeInfoResolver, writeMetadata);
                    break;
                case var t when t == typeof(string):
                    writer.WriteStartObject();
                    writeMetadata?.Invoke();
                    writer.WriteString("type", "string");
                    writer.WriteEndObject();
                    break;
                case var t when t == typeof(bool):
                    writer.WriteStartObject();
                    writeMetadata?.Invoke();
                    writer.WriteString("type", "boolean");
                    writer.WriteEndObject();
                    break;
                case var t when t == typeof(int):
                case var a when a == typeof(uint):
                case var b when b == typeof(long):
                case var c when c == typeof(ulong):
                    writer.WriteStartObject();
                    writeMetadata?.Invoke();
                    writer.WriteString("type", "integer");
                    writer.WriteEndObject();
                    break;
                case var f when f == typeof(float):
                case var d when d == typeof(double):
                    writer.WriteStartObject();
                    writeMetadata?.Invoke();
                    writer.WriteString("type", "number");
                    writer.WriteEndObject();
                    break;
                case var t when t.IsEnum:
                    writer.WriteStartObject();
                    writeMetadata?.Invoke();
                    writer.WritePropertyName("enum");
                    writer.WriteStartArray();
                    foreach (var name in Enum.GetNames(t))
                    {
                        writer.WriteStringValue(name);
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                    break;
                case var t when t.IsArray:
                    writer.WriteStartObject();
                    writeMetadata?.Invoke();
                    writer.WriteString("type", "array");
                    writer.WritePropertyName("items");
                    GenerateSchemaForType(writer, t.GetElementType()!, jsonTypeInfoResolver, writeMetadata);
                    writer.WriteEndObject();
                    break;
                case var t when t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(List<>):
                    writer.WriteStartObject();
                    writeMetadata?.Invoke();
                    writer.WriteString("type", "array");
                    writer.WritePropertyName("items");
                    GenerateSchemaForType(writer, t.GetGenericArguments()[0], jsonTypeInfoResolver, writeMetadata);
                    writer.WriteEndObject();
                    break;
                case var t when t.IsConstructedGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>):
                    // @note: The key of the dictionary has to be strings, because that
                    // is the only thing permitted by JSON.
                    writer.WriteStartObject();
                    writeMetadata?.Invoke();
                    writer.WriteString("type", "object");
                    writer.WritePropertyName("additionalProperties");
                    GenerateSchemaForType(writer, t.GetGenericArguments()[1], jsonTypeInfoResolver, writeMetadata);
                    writer.WriteEndObject();
                    break;
                case var t when jsonTypeInfoResolver.GetTypeInfo(t)?.Kind == JsonTypeInfoKind.Object:
                    GenerateSchemaForObject(
                        writer,
                        jsonTypeInfoResolver.GetTypeInfo(type)!,
                        jsonTypeInfoResolver);
                    break;
                default:
                    throw new InvalidOperationException($"{type.FullName} not supported by schema generator!");
            }
        }
    }
}
