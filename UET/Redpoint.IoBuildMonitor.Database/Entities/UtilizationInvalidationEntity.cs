namespace Io.Database.Entities
{
    using NodaTime;
    using System;

    public class UtilizationInvalidationEntity
    {
        public long Id { get; set; }

        public Instant? Timestamp { get; set; }
    }
}
