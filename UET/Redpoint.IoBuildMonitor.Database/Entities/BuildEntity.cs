namespace Io.Database
{
    using Io.Database.Entities;
    using NodaTime;
    using System.Diagnostics.CodeAnalysis;

    public class BuildEntity : IHasId, IUpdatedByWebhookEvent
    {
        public long Id { get; set; }

        public PipelineEntity? Pipeline { get; set; }

        public string? Stage { get; set; }

        public string? Name { get; set; }

        public string? Status { get; set; }

        public Instant? CreatedAt { get; set; }

        public Instant? StartedAt { get; set; }

        public Instant? FinishedAt { get; set; }

        public long? Duration { get; set; }

        public string? When { get; set; }

        public bool? Manual { get; set; }

        public bool? AllowFailure { get; set; }

        public UserEntity? User { get; set; }

        public RunnerEntity? Runner { get; set; }

        public long? RunnerId { get; set; }

        public PipelineEntity? DownstreamPipeline { get; set; }

        public long? DownstreamPipelineId { get; set; }

        public string? ArtifactsFilename { get; set; }

        public long? ArtifactsSize { get; set; }

        public long LastUpdatedByWebhookEventId { get; set; }

        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Database entity.")]
        public string[] RanWithTags { get; set; } = [];

        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Database entity.")]
        public List<TestEntity>? Tests { get; set; }
    }
}
