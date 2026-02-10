namespace Io.Processor.Periodic
{
    using Io.Database;
    using Io.Database.Utilities;
    using Io.Redis;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NodaTime;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class UtilizationDataProcessor : PeriodicProcessor
    {
        private readonly ILogger<UtilizationDataProcessor> _logger;
        private readonly ITimestampTruncation _timestampTruncation;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public UtilizationDataProcessor(
            ILogger<PeriodicProcessor> baseLogger,
            ILogger<UtilizationDataProcessor> logger,
            ITimestampTruncation timestampTruncation,
            IServiceScopeFactory serviceScopeFactory) : base(baseLogger)
        {
            _logger = logger;
            _timestampTruncation = timestampTruncation;
            _serviceScopeFactory = serviceScopeFactory;
        }

        private static (Instant instant, string id) PickEarliestTime(params (Instant? instant, string id)[] times)
        {
            var instant = Instant.MaxValue;
            var selectedId = string.Empty;
            var didSelect = false;
            foreach (var time in times)
            {
                if (time.instant.HasValue && time.instant.Value < instant)
                {
                    selectedId = time.id;
                    instant = time.instant.Value;
                    didSelect = true;
                }
            }
            if (!didSelect)
            {
                throw new InvalidOperationException("PickEarliestTime: Expected there to be at least one entry and at least one entry to have a non-null instant!");
            }
            return (instant, selectedId);
        }

        protected override async Task ExecuteAsync(long iteration, CancellationToken cancellationToken)
        {
            string selectedGenerationType;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IoDbContext>();

                var currentMinute = _timestampTruncation.TruncateToMinute(SystemClock.Instance.GetCurrentInstant());

                const int maximumMinutes = 1440;
                const int minimumMinutes = 5;

                // Check to see if we have any invalidation entities.
                var invalidationEntities = await dbContext.UtilizationInvalidation.ToListAsync(cancellationToken: cancellationToken);
                var earliestInvalidation = invalidationEntities.Where(x => x.Timestamp != null).Min(x => x.Timestamp);

                // Check to see if we're backfilling history.
                var latestUtilizationMinute = dbContext.UtilizationMinutes.OrderByDescending(x => x.Timestamp).FirstOrDefault();
                Instant? earliestBuildTimestamp = null;
                if (latestUtilizationMinute == null)
                {
                    var firstBuild = dbContext.TimestampedBuilds.OrderBy(x => x.CreatedAt).FirstOrDefault();
                    if (firstBuild == null || firstBuild.CreatedAt == null)
                    {
                        // We don't have any builds yet; don't bother trying
                        // to generate utilization data for another minute.
                        await Task.Delay(60000, cancellationToken);
                        return;
                    }

                    earliestBuildTimestamp = _timestampTruncation.TruncateToMinute(firstBuild.CreatedAt.Value);
                }

                // Check if we're just filling in the last 5 minutes to keep graphs up to date.
                var recentMinutes = _timestampTruncation.TruncateToMinute(currentMinute + Duration.FromMinutes(-minimumMinutes));

                // Compute what time range we're going to process.
                var (earliestTime, generationType) = PickEarliestTime(
                    (earliestInvalidation, "invalidation-entity"),
                    (latestUtilizationMinute?.Timestamp, "backfill-history"),
                    (earliestBuildTimestamp, "backfill-first-batch"),
                    (recentMinutes, "update-recent-minutes")
                );

                // If the earliest time is more than maximum minutes ago, set the end time based
                // on maximum minutes, otherwise set it to the current time.
                Instant startTime;
                Instant endTime;
                if (earliestTime < currentMinute - Duration.FromMinutes(maximumMinutes))
                {
                    startTime = earliestTime;
                    endTime = _timestampTruncation.TruncateToMinute(startTime + Duration.FromMinutes(maximumMinutes));
                }
                else if (earliestTime > currentMinute - Duration.FromMinutes(minimumMinutes))
                {
                    startTime = _timestampTruncation.TruncateToMinute(currentMinute - Duration.FromMinutes(minimumMinutes));
                    endTime = currentMinute;
                }
                else
                {
                    startTime = earliestTime;
                    endTime = currentMinute;
                }

                // Always generate earlier than the start time, in case the start time was picked due to entity invalidation. This
                // ensures zero entries get created in the immediate prior minutes so we don't get large gradients in the graph.
                startTime = startTime - Duration.FromMinutes(minimumMinutes);

                _logger.LogInformation($"Generating utilization data from {startTime} to {endTime} ({generationType})...");

                // Generate utilization data.
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""UtilizationMinutes""
    WITH ""Results"" AS (
        SELECT
            ""Timestamp"",
            ""RunnerTags"".""RunnerTag"" AS ""RunnerTag"",
            COUNT(""TimestampedBuilds"".""BuildId"") FILTER (WHERE ""TimestampedBuilds"".""CreatedAt"" <= ""Timestamp"" AND ""TimestampedBuilds"".""PendingAt"" > ""Timestamp"") AS ""Created"",
            COUNT(""TimestampedBuilds"".""BuildId"") FILTER (WHERE ""TimestampedBuilds"".""PendingAt"" <= ""Timestamp"" AND ""TimestampedBuilds"".""RunningAt"" > ""Timestamp"") AS ""Pending"",
            COUNT(""TimestampedBuilds"".""BuildId"") FILTER (WHERE ""TimestampedBuilds"".""RunningAt"" <= ""Timestamp"" AND ""TimestampedBuilds"".""FinishedAt"" > ""Timestamp"") AS ""Running""
        FROM (SELECT DISTINCT UNNEST(""Tags"") AS ""RunnerTag"" FROM ""Runners"") AS ""RunnerTags""
        LEFT JOIN GENERATE_SERIES({startTime}, {endTime}, '1 minute'::INTERVAL) ""Timestamp"" 
	  	    ON 1 = 1
        LEFT JOIN ""TimestampedBuilds""
            ON ""TimestampedBuilds"".""CreatedAt"" <= ""Timestamp""
        AND ""TimestampedBuilds"".""FinishedAt"" > ""Timestamp""
        AND ""RunnerTags"".""RunnerTag"" = ANY(""TimestampedBuilds"".""RanWithTags"")
        GROUP BY ""Timestamp"", ""RunnerTags"".""RunnerTag""
        ORDER BY ""Timestamp"" ASC, ""RunnerTags"".""RunnerTag"" ASC
    )
    SELECT
        ""Timestamp"",
        ""RunnerTag"",
        ""Created"",
        ""Pending"",
        ""Running""
    FROM ""Results""
ON CONFLICT (""Timestamp"", ""RunnerTag"")
DO 
UPDATE SET ""RunnerTag"" = EXCLUDED.""RunnerTag"", 
           ""Created"" = EXCLUDED.""Created"", 
           ""Pending"" = EXCLUDED.""Pending"", 
           ""Running"" = EXCLUDED.""Running""
;
", cancellationToken: cancellationToken);
                // If we updated based on invalidation entities, clear out any that were within the time period.
                var requiresSave = false;
                if (generationType == "invalidation-entity")
                {
                    foreach (var invalidationEntity in invalidationEntities)
                    {
                        if (invalidationEntity.Timestamp == null ||
                            invalidationEntity.Timestamp <= endTime)
                        {
                            dbContext.UtilizationInvalidation.Remove(invalidationEntity);
                            requiresSave = true;
                        }
                    }
                }

                // If we've updated the last 5 minute block, also vacuum up any rows that we
                // no longer need.
                if (generationType == "update-recent-minutes")
                {
                    var affectedRows = await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
DELETE FROM ""UtilizationMinutes""
WHERE CTID IN (
	WITH ""Data"" AS (
		SELECT 
			CTID,
			""Timestamp"",
			""RunnerTag"",
			""Created"",
			""Pending"",
			""Running"",
			""Created"" + ""Pending"" + ""Running"" AS ""Total"",
			MAX(""Created"" + ""Pending"" + ""Running"") OVER (PARTITION BY ""RunnerTag"" ORDER BY ""Timestamp"" ASC ROWS BETWEEN 5 PRECEDING AND 5 FOLLOWING) AS ""NearTotal""
		FROM ""UtilizationMinutes""
		WHERE ""Timestamp"" < {startTime}
		ORDER BY ""RunnerTag"" ASC, ""Timestamp"" ASC
	)
	SELECT CTID FROM ""Data"" WHERE ""NearTotal"" = 0
)", cancellationToken: cancellationToken);

                    _logger.LogInformation($"Vacuumed {affectedRows} unnecessary rows from utilization table after generation.");
                }

                // Save all of our changes to invalidation entities.
                if (requiresSave)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                // Only send the notification when we do recent minutes, since other update types will loop immediately and eventually
                // get to processing recent minutes very quickly.
                if (generationType == "update-recent-minutes")
                {
                    await scope.ServiceProvider.GetRequiredService<INotificationHub>().NotifyAsync(NotificationType.UtilizationUpdated);
                }

                // Set generation type so we know whether or not to delay after execution.
                selectedGenerationType = generationType;
            }

            if (selectedGenerationType == "update-recent-minutes")
            {
                // We've generated utilization data, no need to run for another minute.
                await Task.Delay(60000, cancellationToken);
            }
        }
    }
}
