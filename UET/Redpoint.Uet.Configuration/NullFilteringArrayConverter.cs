namespace Redpoint.Uet.Configuration
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;

    internal sealed class NullFilteringArrayConverter<T> : JsonConverter<T[]>
    {
        public override T[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected entry to be a JSON array.");
            }
            var result = new List<T>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                var v = JsonSerializer.Deserialize<T>(ref reader, (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)));
                if (v != null)
                {
                    result.Add(v);
                }
            }
            return result.ToArray();
        }

        public override void Write(Utf8JsonWriter writer, T[] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var v in value)
            {
                if (v != null)
                {
                    JsonSerializer.Serialize<T>(writer, v, (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)));
                }
            }
            writer.WriteEndArray();
        }
    }
}
