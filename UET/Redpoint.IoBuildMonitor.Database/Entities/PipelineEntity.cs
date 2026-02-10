namespace Io.Database
{
    using NodaTime;
    using System.Diagnostics.CodeAnalysis;

    public class PipelineEntity : IHasId, IUpdatedByWebhookEvent
    {
        public long Id { get; set; }

        public string? Ref { get; set; }

        public bool? Tag { get; set; }

        public string? Sha { get; set; }

        public string? PreviousSha { get; set; }

        public string? Source { get; set; }

        public string? Status { get; set; }

        public string? DetailedStatus { get; set; }

        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Database entity.")]
        public string[] Stages { get; set; } = [];

        public Instant? CreatedAt { get; set; }

        public Instant? FinishedAt { get; set; }

        public long? Duration { get; set; }

        public long? QueuedDuration { get; set; }

        public MergeRequestEntity? MergeRequest { get; set; }

        public UserEntity? User { get; set; }

        public ProjectEntity? Project { get; set; }

        public CommitEntity? Commit { get; set; }

        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Database entity.")]
        public List<BuildEntity> Builds { get; set; } = new List<BuildEntity>();

        public BuildEntity? UpstreamBuild { get; set; }

        public long LastUpdatedByWebhookEventId { get; set; }
    }
}
