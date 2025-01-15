namespace Redpoint.CloudFramework.Repository.Converters.JsonHelpers
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;

    internal class VersionedJsonConverter : JsonConverter
    {
        [SuppressMessage("Trimming", "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.", Justification = "This API does not support trimmed applications.")]
        public override bool CanConvert(Type objectType)
        {
            // Check if the type has a SchemaVersion attribute.
            var schemaVersionAttributes = objectType.GetCustomAttributes(typeof(SchemaVersionAttribute), false).OfType<SchemaVersionAttribute>().ToArray();
            if (schemaVersionAttributes.Length == 0)
            {
                return false;
            }
            else
            {
                // Check that the code has a deserializer for every version.
                var schemaVersion = schemaVersionAttributes[0].SchemaVersion;
                for (var version = 1u; version < schemaVersion; version++)
                {
                    if (objectType.GetMethod("DeserializeFromVersion" + version, BindingFlags.Public | BindingFlags.Static) == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        [SuppressMessage("Trimming", "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The parameter of method does not have matching annotations.", Justification = "This API does not support trimmed applications.")]
        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var obj = JToken.ReadFrom(reader);

            var codeVersion = objectType.GetCustomAttributes(typeof(SchemaVersionAttribute), false).OfType<SchemaVersionAttribute>().First().SchemaVersion;

            if (obj.Type != JTokenType.Object)
            {
                return obj.ToObject(objectType);
            }

            var schemaVersion = obj["$rcf$schemaVersion"];
            if (schemaVersion == null)
            {
                return obj.ToObject(objectType);
            }

            var dataVersion = schemaVersion.ToObject<long>();

            if (codeVersion == dataVersion)
            {
                return obj.ToObject(objectType);
            }

            // Schema version doesn't match, ask to code to deserialize from older version.
            var deserializer = objectType.GetMethod("DeserializeFromVersion" + dataVersion, BindingFlags.Public | BindingFlags.Static)!;
            return deserializer.Invoke(null, new[] { obj.ToString() });
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var codeVersion = value.GetType().GetCustomAttributes(typeof(SchemaVersionAttribute), false).OfType<SchemaVersionAttribute>().First().SchemaVersion;

            var token = JToken.FromObject(value);

            if (token.Type != JTokenType.Object)
            {
                token.WriteTo(writer);
            }
            else
            {
                var obj = (JObject)token;
                obj.AddFirst(new JProperty("$rcf$schemaVersion", codeVersion));
                obj.WriteTo(writer);
            }
        }
    }
}
