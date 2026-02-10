using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Io.Database
{
    [Keyless]
    public class PipelineEstimations
    {
        public long PipelineId { get; set; }

        public PipelineEntity? Pipeline { get; set; }

        public Instant? EstimatedFinishedAt { get; set; }
    }
}
