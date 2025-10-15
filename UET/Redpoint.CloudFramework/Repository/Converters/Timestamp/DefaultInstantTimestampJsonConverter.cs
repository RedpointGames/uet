namespace Redpoint.CloudFramework.Repository.Converters.Timestamp
{
    using NodaTime;
    using Redpoint.CloudFramework.Repository.Converters.JsonHelpers;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization.Metadata;

    internal class DefaultInstantTimestampJsonConverter : IInstantTimestampJsonConverter
    {
        public Instant? FromJsonCacheToNodaTimeInstant(JsonNode? obj)
        {
            if (obj == null || obj.GetValueKind() != JsonValueKind.Object)
            {
                return null;
            }

            if (obj.AsObject().TryGetPropertyValue("seconds", out var seconds) &&
                obj.AsObject().TryGetPropertyValue("nanos", out var nanos))
            {
                return Instant.FromUnixTimeSeconds(seconds?.GetValue<long>() ?? 0).PlusNanoseconds(nanos?.GetValue<long>() ?? 0);
            }

            return null;
        }

        public JsonNode? FromNodaTimeInstantToJsonCache(Instant? instant)
        {
            if (instant == null)
            {
                return null;
            }

            var seconds = instant.Value.ToUnixTimeSeconds();
            var nanos = (instant.Value - Instant.FromUnixTimeSeconds(instant.Value.ToUnixTimeSeconds())).SubsecondNanoseconds;

            return new JsonObject
            {
                { "seconds", JsonValue.Create((long)seconds) },
                { "nanos", JsonValue.Create((long)nanos) },
            };
        }
    }
}