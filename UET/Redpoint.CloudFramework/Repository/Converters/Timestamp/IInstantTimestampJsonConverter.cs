namespace Redpoint.CloudFramework.Repository.Converters.Timestamp
{
    using NodaTime;
    using System.Text.Json.Nodes;

    internal interface IInstantTimestampJsonConverter
    {
        Instant? FromJsonCacheToNodaTimeInstant(JsonNode? obj);
        JsonNode? FromNodaTimeInstantToJsonCache(Instant? instant);
    }
}