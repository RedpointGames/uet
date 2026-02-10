namespace Io.Database
{
    using System.Diagnostics.CodeAnalysis;

    public class UserEntity : IHasId, IUpdatedByWebhookEvent
    {
        public long Id { get; set; }

        public string? Name { get; set; }

        public string? Username { get; set; }

        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Database entity.")]
        public string? AvatarUrl { get; set; }

        public string? Email { get; set; }

        public long LastUpdatedByWebhookEventId { get; set; }
    }
}
