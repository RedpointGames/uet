namespace Io.Database.Utilities
{
    using NodaTime;
    using System;

    internal class TimestampTruncation : ITimestampTruncation
    {
        public Instant TruncateToMinute(Instant timestamp)
        {
            var input = timestamp.ToDateTimeOffset();
            return Instant.FromDateTimeOffset(new DateTimeOffset(input.Year, input.Month, input.Day, input.Hour, input.Minute, 0, input.Offset));
        }
    }
}
