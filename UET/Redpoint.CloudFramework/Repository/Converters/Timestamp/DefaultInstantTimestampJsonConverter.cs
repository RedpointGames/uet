namespace Redpoint.CloudFramework.Repository.Converters.Timestamp
{
    using Newtonsoft.Json.Linq;
    using NodaTime;

    internal class DefaultInstantTimestampJsonConverter : IInstantTimestampJsonConverter
    {
        public Instant? FromJsonCacheToNodaTimeInstant(JToken? obj)
        {
            if (obj == null || obj?.Type == JTokenType.Null)
            {
                return null;
            }

            return Instant.FromUnixTimeSeconds(obj!["seconds"]?.Value<long>() ?? 0).PlusNanoseconds(obj!["nanos"]?.Value<long>() ?? 0);
        }

        public JToken FromNodaTimeInstantToJsonCache(Instant? instant)
        {
            if (instant == null)
            {
                return JValue.CreateNull();
            }

            var seconds = instant.Value.ToUnixTimeSeconds();
            var nanos = (instant.Value - Instant.FromUnixTimeSeconds(instant.Value.ToUnixTimeSeconds())).SubsecondNanoseconds;
            var obj = new JObject();
            obj["seconds"] = (long)seconds;
            obj["nanos"] = (long)nanos;
            return obj;
        }
    }
}