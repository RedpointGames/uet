namespace Io.Database.Views
{
    using Microsoft.EntityFrameworkCore;
    using System.Diagnostics.CodeAnalysis;

    [Keyless]
    public class ProjectHealths
    {
        public long ProjectId { get; set; }

        public ProjectEntity? Project { get; set; }

        public string? Name { get; set; }

        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Database entity.")]
        public string? WebUrl { get; set; }

        public string? DefaultBranch { get; set; }

        public long PipelineId { get; set; }

        public PipelineEntity? Pipeline { get; set; }

        public string? Status { get; set; }

        public string? Sha { get; set; }
    }
}
