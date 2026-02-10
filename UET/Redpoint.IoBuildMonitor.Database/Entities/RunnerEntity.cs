namespace Io.Database
{
    using System.Diagnostics.CodeAnalysis;

    public class RunnerEntity : IHasId, IUpdatedByWebhookEvent
    {
        public long Id { get; set; }

        public string? Description { get; set; }

        public bool? Active { get; set; }

        public string? RunnerType { get; set; }

        public bool? IsShared { get; set; }

        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Database entity.")]
        public string[] Tags { get; set; } = [];

        [SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Database entity.")]
        public List<BuildEntity> Builds { get; set; } = new List<BuildEntity>();

        public long LastUpdatedByWebhookEventId { get; set; }
    }
}
