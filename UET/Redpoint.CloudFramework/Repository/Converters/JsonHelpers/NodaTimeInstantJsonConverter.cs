namespace Redpoint.CloudFramework.Repository.Converters.JsonHelpers
{
    using NodaTime;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class NodaTimeInstantJsonConverter : JsonConverter<Instant>
    {
        public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            long seconds = 0;
            long nanos = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return Instant.FromUnixTimeSeconds(seconds).PlusNanoseconds(nanos);
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                var propertyName = reader.GetString();

                reader.Read();
                if (propertyName == "seconds")
                {
                    seconds = reader.GetInt64();
                }
                else if (propertyName == "nanos")
                {
                    nanos = reader.GetInt64();
                }
            }

            return Instant.MinValue;
        }

        public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
        {
            var seconds = value.ToUnixTimeSeconds();
            var nanos = (value - Instant.FromUnixTimeSeconds(seconds)).SubsecondNanoseconds;

            writer.WriteStartObject();
            writer.WriteNumber("seconds", seconds);
            writer.WriteNumber("nanos", nanos);
            writer.WriteEndObject();
        }
    }
}
