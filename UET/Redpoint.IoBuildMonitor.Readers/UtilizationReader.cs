namespace Io.Readers
{
    using Io.Database;
    using Io.Json.Frontend;
    using Microsoft.EntityFrameworkCore;
    using NodaTime;
    using System.Threading.Tasks;

    public class UtilizationReader : IUtilizationReader
    {
        private readonly IoDbContext _db;

        public UtilizationReader(IoDbContext db)
        {
            _db = db;
        }

        public async Task<UtilizationStats> ReadAsync()
        {
            var endTime = SystemClock.Instance.GetCurrentInstant();
            var startTime = endTime - Duration.FromDays(1);

            var stats = await _db.UtilizationMinutes
                .Where(x => x.Timestamp!.Value >= startTime && x.Timestamp.Value <= endTime)
                .OrderBy(x => x.Timestamp)
                .ToListAsync();
            var runners = await _db.Runners.ToListAsync();
            var desiredCapacities = (await _db.DesiredCapacityCalculations.ToListAsync())
                .GroupBy(x => x.RunnerTag)
                .ToDictionary(k => k.Key!, v => v.ToList());

            return new UtilizationStats
            {
                RunnerUtilizationStats = stats.GroupBy(x => x.RunnerTag).Select(x =>
                {
                    var capacity = runners.Count(y => y.Tags.Contains(x.Key));
                    return new RunnerUtilizationStats
                    {
                        Tag = x.Key,
                        Capacity = capacity,
                        Datapoints = x.Select(x => new RunnerUtilizationStatsDatapoint
                        {
                            TimestampMinute = x.Timestamp!.Value.ToUnixTimeSeconds() / 60,
                            Created = x.Created ?? 0,
                            Pending = x.Pending ?? 0,
                            Running = x.Running ?? 0,
                        }).ToList(),
                        DesiredCapacity = desiredCapacities.TryGetValue(x.Key!, out var desiredCapacity1) ? (long)Math.Round(desiredCapacity1.FirstOrDefault(x => x.Percentile == 0.500)!.DesiredCapacity) : capacity,
                        DesiredCapacityDistribution = desiredCapacities.TryGetValue(x.Key!, out var desiredCapacity2) ? desiredCapacity2.Select(x => new
                        RunnerUtilizationStatsCapacityDistribution
                        {
                            Percentile = x.Percentile,
                            DesiredCapacity = x.DesiredCapacity,
                        }).ToArray() : [],
                    };
                }).OrderBy(x => x.Tag!.Count(x => x == '-')).ThenByDescending(x => x.Tag).ToList(),
            };
        }
    }
}