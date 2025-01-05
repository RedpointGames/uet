namespace Redpoint.CloudFramework.Repository.Converters.Timestamp
{
    using Newtonsoft.Json.Linq;
    using NodaTime;

    internal interface IInstantTimestampJsonConverter
    {
        Instant? FromJsonCacheToNodaTimeInstant(JToken? obj);
        JToken FromNodaTimeInstantToJsonCache(Instant? instant);
    }
}