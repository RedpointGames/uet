namespace Redpoint.CloudFramework.OpenApi
{
    using NodaTime;
    using NodaTime.Text;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class InstantJsonConverter : JsonConverter<Instant>
    {
        public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return OffsetDateTimePattern.Rfc3339.Parse(reader.GetString()!).Value.ToInstant();
        }

        public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteStringValue(OffsetDateTimePattern.Rfc3339.Format(value.WithOffset(Offset.Zero)));
        }
    }
}
