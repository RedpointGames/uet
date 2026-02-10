namespace Io.Database.Entities
{
    using NodaTime;
    using System;
    using System.ComponentModel.DataAnnotations;

    public class UtilizationMinuteEntity
    {
        public Instant? Timestamp { get; set; }

        public string? RunnerTag { get; set; }

        public long? Created { get; set; }

        public long? Pending { get; set; }

        public long? Running { get; set; }
    }
}
