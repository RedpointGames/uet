namespace Io.Database
{
    using NodaTime;

    public class BuildStatusChangeEntity
    {
        public long Id { get; set; }

        public BuildEntity? Build { get; set; }

        public Instant? StatusChangedAt { get; set; }

        public string? OldStatus { get; set; }

        public string? NewStatus { get; set; }
    }
}
