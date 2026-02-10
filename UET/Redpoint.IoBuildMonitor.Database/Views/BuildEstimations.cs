using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace Io.Database
{
    [Keyless]
    public class BuildEstimations
    {
        public long BuildId { get; set; }

        public BuildEntity? Build { get; set; }

        public Instant? EstimatedFinishedAt { get; set; }
    }
}
