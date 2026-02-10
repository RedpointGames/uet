namespace Io.Database.Entities
{
    using NodaTime;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class TestEntity : IHasId
    {
        public static string ComputeLookup(long buildId, string? fullName, string? platform, string? instance)
        {
            return buildId + ":" + fullName + ":" + platform + ":" + instance;
        }

        public long Id { get; set; }

#pragma warning disable CS8618
        public string LookupId { get; set; }
#pragma warning restore CS8618

        public string? DurationEstimationHash { get; set; }

        public BuildEntity? Build { get; set; }

        public long BuildId { get; set; }

        public string? FullName { get; set; }

        public string? Platform { get; set; }

        public string? AutomationInstance { get; set; }

        public string? GauntletInstance { get; set; }

        public string? Status { get; set; }

        public bool? IsGauntlet { get; set; }

        public Instant? DateCreatedUtc { get; set; }

        public Instant? DateStartedUtc { get; set; }

        public Instant? DateFinishedUtc { get; set; }

        public double? DurationSeconds { get; set; }
    }
}
