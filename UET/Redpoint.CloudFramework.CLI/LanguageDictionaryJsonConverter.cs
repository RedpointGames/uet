namespace Redpoint.CloudFramework.CLI
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class LanguageDictionaryJsonConverter : JsonConverter<LanguageDictionary>
    {
        public override LanguageDictionary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = new LanguageDictionary();
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString()!;
                    reader.Read();
                    var propertyValue = reader.GetString()!;
                    value.Add(propertyName, propertyValue);
                }
            }
            return value;
        }

        public override void Write(Utf8JsonWriter writer, LanguageDictionary value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kv in value)
            {
                writer.WritePropertyName(kv.Key);
                writer.WriteStringValue(kv.Value);
            }
            writer.WriteEndObject();
        }
    }
}
