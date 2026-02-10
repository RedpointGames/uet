namespace Io.Database
{
    using System.Diagnostics.CodeAnalysis;

    public class CommitEntity : IUpdatedByWebhookEvent
    {
        public string? Id { get; set; }

        public string? Message { get; set; }

        public string? Timestamp { get; set; }

        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Database entity.")]
        public string? Url { get; set; }

        public string? AuthorName { get; set; }

        public string? AuthorEmail { get; set; }

        public long LastUpdatedByWebhookEventId { get; set; }
    }
}
