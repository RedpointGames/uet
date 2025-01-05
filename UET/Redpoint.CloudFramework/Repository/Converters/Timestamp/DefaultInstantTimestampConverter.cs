namespace Redpoint.CloudFramework.Repository.Converters.Timestamp
{
    using Google.Protobuf.WellKnownTypes;
    using NodaTime;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal class DefaultInstantTimestampConverter : IInstantTimestampConverter
    {
        public Instant? FromDatastoreValueToNodaTimeInstant(Value value)
        {
            if (value?.TimestampValue == null)
            {
                return null;
            }

            return Instant.FromUnixTimeSeconds(value.TimestampValue.Seconds) + NodaTime.Duration.FromNanoseconds(value.TimestampValue.Nanos);
        }

        public Value FromNodaTimeInstantToDatastoreValue(Instant? instant, bool excludeFromIndexes)
        {
            if (instant == null)
            {
                return Value.ForNull();
            }

            return new Value
            {
                TimestampValue = new Timestamp
                {
                    Seconds = instant.Value.ToUnixTimeSeconds(),
                    Nanos = (instant.Value - Instant.FromUnixTimeSeconds(instant.Value.ToUnixTimeSeconds())).SubsecondNanoseconds,
                },
                ExcludeFromIndexes = excludeFromIndexes,
            };
        }
    }
}
