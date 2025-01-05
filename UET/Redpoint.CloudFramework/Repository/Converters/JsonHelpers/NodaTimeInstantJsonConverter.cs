namespace Redpoint.CloudFramework.Repository.Converters.JsonHelpers
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NodaTime;
    using System;

    internal class NodaTimeInstantJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Instant);
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            var obj = JToken.ReadFrom(reader);

            if (obj == null || obj?.Type == JTokenType.Null)
            {
                return null;
            }

            return Instant.FromUnixTimeSeconds(obj!["seconds"]?.Value<long>() ?? 0).PlusNanoseconds(obj!["nanos"]?.Value<long>() ?? 0);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            var instant = (Instant?)value;

            if (instant == null)
            {
                JValue.CreateNull().WriteTo(writer);
                return;
            }

            var seconds = instant.Value.ToUnixTimeSeconds();
            var nanos = (instant.Value - Instant.FromUnixTimeSeconds(instant.Value.ToUnixTimeSeconds())).SubsecondNanoseconds;
            var obj = new JObject();
            obj["seconds"] = (long)seconds;
            obj["nanos"] = (long)nanos;
            obj.WriteTo(writer);
        }
    }
}
