namespace Redpoint.CloudFramework.Repository.Converters.Timestamp
{
    using Google.Cloud.Datastore.V1;
    using NodaTime;

    public interface IInstantTimestampConverter
    {
        Instant? FromDatastoreValueToNodaTimeInstant(Value value);
        Value FromNodaTimeInstantToDatastoreValue(Instant? instant, bool excludeFromIndexes);
    }
}