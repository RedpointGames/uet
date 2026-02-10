namespace Io.Database
{
    using System.Diagnostics.CodeAnalysis;

    public class MergeRequestEntity : IHasId, IUpdatedByWebhookEvent
    {
        public long Id { get; set; }

        public long InternalId { get; set; }

        public string? Title { get; set; }

        public string? SourceBranch { get; set; }

        public long? SourceProjectId { get; set; }

        public string? TargetBranch { get; set; }

        public long? TargetProjectId { get; set; }

        public string? State { get; set; }

        public string? MergeStatus { get; set; }

        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Database entity.")]
        public string? Url { get; set; }

        public long LastUpdatedByWebhookEventId { get; set; }
    }
}
