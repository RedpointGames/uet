namespace Io.Database
{
    using System.Diagnostics.CodeAnalysis;

    public class ProjectEntity : IHasId, IUpdatedByWebhookEvent
    {
        public long Id { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Database entity.")]
        public string? WebUrl { get; set; }

        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Database entity.")]
        public string? AvatarUrl { get; set; }

        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Database entity.")]
        public string? GitSshUrl { get; set; }

        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Database entity.")]
        public string? GitHttpUrl { get; set; }

        public string? Namespace { get; set; }

        public long? VisibilityLevel { get; set; }

        public string? PathWithNamespace { get; set; }

        public string? DefaultBranch { get; set; }

        public long LastUpdatedByWebhookEventId { get; set; }
    }
}
