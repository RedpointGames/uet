namespace Redpoint.Unreal.Serialization
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class UnrealGuidConverter : JsonConverter<Guid>
    {
        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            int a = 0, b = 0, c = 0, d = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                var propertyName = reader.GetString();

                reader.Read();

                switch (propertyName)
                {
                    case "A":
                        a = reader.GetInt32();
                        break;
                    case "B":
                        b = reader.GetInt32();
                        break;
                    case "C":
                        c = reader.GetInt32();
                        break;
                    case "D":
                        d = reader.GetInt32();
                        break;
                }
            }

            return ArchiveGuid.GuidFromInts(a, b, c, d);
        }

        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            var (a, b, c, d) = ArchiveGuid.IntsFromGuid(value);

            writer.WriteStartObject();
            writer.WriteNumber("A", a);
            writer.WriteNumber("B", b);
            writer.WriteNumber("C", c);
            writer.WriteNumber("D", d);
            writer.WriteEndObject();
        }
    }
}
