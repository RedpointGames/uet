namespace Io.Database.Views
{
    using Microsoft.EntityFrameworkCore;
    using NodaTime;
    using System.Diagnostics.CodeAnalysis;

    [Keyless]
    public class TimestampedBuilds
    {
        public long BuildId { get; set; }

        public BuildEntity? Build { get; set; }

        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Database entity.")]
        public string[] RanWithTags { get; set; } = [];

        public Instant? CreatedAt { get; set; }

        public Instant? PendingAt { get; set; }

        public Instant? RunningAt { get; set; }

        public Instant? FinishedAt { get; set; }
    }
}
