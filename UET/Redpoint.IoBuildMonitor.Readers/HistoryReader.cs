namespace Io.Readers
{
    using Io.Database;
    using Io.Json.Frontend;
    using Microsoft.EntityFrameworkCore;
    using NodaTime;
    using System.Threading.Tasks;

    internal class HistoryReader : IHistoryReader
    {
        private readonly IoDbContext _db;

        public HistoryReader(IoDbContext db)
        {
            _db = db;
        }

        private async Task<List<PipelineStats>> ReadRecentPipelineStats()
        {
            // Find top-level recent pipelines, then recursively find all of the
            // related downstream pipelines.
            var last96Hours = SystemClock.Instance.GetCurrentInstant() - Duration.FromHours(96);
            var pipelinesToProcess = await _db.Pipelines
                .Include(x => x.Builds).ThenInclude(x => x.DownstreamPipeline)
                .Include(x => x.Builds).ThenInclude(x => x.Tests)
                .Include(x => x.MergeRequest)
                .Include(x => x.Project)
                .Where(x => ((x.Status == "success" || x.Status == "failed" || x.Status == "canceled") && x.FinishedAt != null && x.FinishedAt >= last96Hours) || (x.Status == "manual" && x.CreatedAt >= last96Hours))
                .Where(x => x.Source != "pipeline" && x.Source != "parent_pipeline")
                .OrderByDescending(x => x.FinishedAt ?? x.CreatedAt)
                .Take(100)
                .ToListAsync();
            var pipelinesToProcessIds = pipelinesToProcess.Select(x => x.Id).ToList();
            var buildsWithPipelinesAsDownstream = (await _db.Builds
                .Where(x => x.DownstreamPipelineId != null && pipelinesToProcessIds.Contains(x.DownstreamPipelineId.Value))
                .Select(x => x.DownstreamPipelineId)
                .Distinct()
                .ToListAsync()).ToHashSet();
            var pipelinesToScan = pipelinesToProcess.Where(x => !buildsWithPipelinesAsDownstream.Contains(x.Id));
            var childPipelineDictionary = new Dictionary<long, PipelineEntity>();
            var didFetch = false;
            do
            {
                didFetch = false;
                var nextPipelineBatchToFetch = new HashSet<long>();
                foreach (var pipeline in pipelinesToScan)
                {
                    foreach (var build in pipeline.Builds)
                    {
                        if (build.DownstreamPipelineId != null)
                        {
                            nextPipelineBatchToFetch.Add(build.DownstreamPipelineId.Value);
                            didFetch = true;
                        }
                    }
                }

                var nextBatch = await _db.Pipelines
                    .Include(x => x.Builds).ThenInclude(x => x.DownstreamPipeline)
                    .Include(x => x.Builds).ThenInclude(x => x.Tests)
                    .Include(x => x.MergeRequest)
                    .Include(x => x.Project)
                    .Where(x => nextPipelineBatchToFetch.Contains(x.Id))
                    .ToListAsync();
                pipelinesToScan = nextBatch;
                foreach (var childPipeline in nextBatch)
                {
                    childPipelineDictionary.Add(childPipeline.Id, childPipeline);
                }
            } while (didFetch);

            PipelineStats ConvertEntityToStats(PipelineEntity? pipeline, long? pipelineFallbackId)
            {
                if (pipeline == null && pipelineFallbackId != null)
                {
                    pipeline = childPipelineDictionary[pipelineFallbackId.Value];
                }

                ArgumentNullException.ThrowIfNull(pipeline);

                var stats = new PipelineStats();
                stats.Id = pipeline.Id;
                stats.Status = pipeline.Status;
                stats.StartedUtcMillis = pipeline.CreatedAt?.ToUnixTimeMilliseconds();
                stats.EstimatedUtcMillis = pipeline.FinishedAt?.ToUnixTimeMilliseconds();
                if (pipeline.Project != null)
                {
                    if (pipeline.MergeRequest != null)
                    {
                        stats.Title = pipeline.Project.Name + " (!" + pipeline.MergeRequest.InternalId + ")";
                    }
                    else
                    {
                        stats.Title = pipeline.Project.Name + " (" + pipeline.Ref + ")";
                    }
                    stats.Url = "https://src.redpoint.games/" + pipeline.Project?.PathWithNamespace + "/-/pipelines/" + pipeline.Id;
                }
                else
                {
                    stats.Title = "#" + pipeline.Id;
                    stats.Url = string.Empty;
                }
                stats.Stages = new List<PipelineStageStats>();

                var buildsByStage = pipeline.Builds.GroupBy(x => x.Stage).ToDictionary(k => k.Key!, v => v.ToList());
                foreach (var stage in (pipeline.Stages ?? []))
                {
                    buildsByStage.TryGetValue(stage, out var buildsInStage);
                    if (buildsInStage == null)
                    {
                        buildsInStage = new();
                    }

                    var stageStats = new PipelineStageStats();
                    stageStats.Name = stage;
                    stageStats.Builds = new List<PipelineBuildStats>();

                    var buildsInStageUniqueByLatest = new Dictionary<string, BuildEntity>();
                    foreach (var build in buildsInStage.OrderBy(x => x.Id))
                    {
                        // This ensures that for each name, we pick the latest iteration of the job. This means
                        // that retries appear over the top of their previous iterations.
                        buildsInStageUniqueByLatest[build.Name!] = build;
                    }

                    foreach (var build in buildsInStageUniqueByLatest.Values)
                    {
                        var buildStats = new PipelineBuildStats();
                        buildStats.Id = build.Id;
                        buildStats.Name = build.Name;
                        buildStats.Url = "https://src.redpoint.games/" + pipeline.Project?.PathWithNamespace + "/-/jobs/" + build.Id;
                        buildStats.Status = build.Status;
                        buildStats.StartedUtcMillis = build.StartedAt?.ToUnixTimeMilliseconds();
                        buildStats.Tests = new List<TestStats>();
                        if (build.Tests != null)
                        {
                            foreach (var test in build.Tests)
                            {
                                buildStats.Tests.Add(new TestStats
                                {
                                    Id = test.LookupId,
                                    FullName = test.FullName,
                                    Status = test.Status,
                                    StartedUtcMillis = test.DateStartedUtc?.ToUnixTimeMilliseconds(),
                                    FinishedUtcMillis = test.DateFinishedUtc?.ToUnixTimeMilliseconds(),
                                });
                            }
                        }
                        if (build.DownstreamPipelineId != null)
                        {
                            buildStats.DownstreamPipeline = ConvertEntityToStats(null, build.DownstreamPipelineId);
                            buildStats.Status = build.DownstreamPipeline!.Status;
                            buildStats.StartedUtcMillis = build.DownstreamPipeline.CreatedAt?.ToUnixTimeMilliseconds();
                            buildStats.EstimatedUtcMillis = build.DownstreamPipeline.FinishedAt?.ToUnixTimeMilliseconds();
                        }
                        else if (build.Status == "pending")
                        {
                            if (build.RunnerId == null)
                            {
                                buildStats.Status = "queued";
                            }
                        }
                        else
                        {
                        }
                        stageStats.Builds.Add(buildStats);
                    }

                    stats.Stages.Add(stageStats);
                }

                return stats;
            }

            // Construct the result set.
            var results = new List<PipelineStats>();
            foreach (var pipeline in pipelinesToProcess.Where(x => !buildsWithPipelinesAsDownstream.Contains(x.Id)))
            {
                results.Add(ConvertEntityToStats(pipeline, null));
            }

            // Return results.
            return results;
        }

        public async Task<HistoryStats> ReadAsync()
        {
            return new HistoryStats
            {
                RecentPipelines = (await ReadRecentPipelineStats()).OrderByDescending(x => x.EstimatedUtcMillis).ToList(),
            };
        }
    }
}