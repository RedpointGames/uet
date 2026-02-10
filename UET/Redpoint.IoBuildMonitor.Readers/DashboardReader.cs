namespace Io.Readers
{
    using Io.Database;
    using Io.Json.Frontend;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal class DashboardReader : IDashboardReader
    {
        private readonly IoDbContext _db;

        public DashboardReader(IoDbContext db)
        {
            _db = db;
        }

        private async Task<List<RunnerStats>> ReadRunnerStats()
        {
            var runners = await _db.Runners
                .Include(x => x.Builds).ThenInclude(x => x.Pipeline)
                .Include(x => x.Builds).ThenInclude(x => x.Pipeline!.MergeRequest)
                .Include(x => x.Builds).ThenInclude(x => x.Pipeline!.Project)
                .Include(x => x.Builds).ThenInclude(x => x.Pipeline!.UpstreamBuild)
                .Include(x => x.Builds).ThenInclude(x => x.Pipeline!.UpstreamBuild!.Pipeline)
                .Include(x => x.Builds).ThenInclude(x => x.Pipeline!.UpstreamBuild!.Pipeline!.Project)
                .Include(x => x.Builds).ThenInclude(x => x.Pipeline!.UpstreamBuild!.Pipeline!.UpstreamBuild)
                .Include(x => x.Builds).ThenInclude(x => x.Pipeline!.UpstreamBuild!.Pipeline!.UpstreamBuild!.Pipeline)
                .Include(x => x.Builds).ThenInclude(x => x.Pipeline!.UpstreamBuild!.Pipeline!.UpstreamBuild!.Pipeline!.Project)
                .ToListAsync();

            var results = new List<RunnerStats>();
            foreach (var runner in runners)
            {
                var stats = new RunnerStats
                {
                    Id = runner.Id,
                    Description = runner.Description,
                    Builds = new List<BuildStats>(),
                };
                foreach (var build in runner.Builds)
                {
                    var anchors = new List<BuildStatsAnchor>();
                    var pipelines = new List<PipelineEntity>();
                    var currentPipeline = build.Pipeline;
                    while (currentPipeline != null)
                    {
                        pipelines.Add(currentPipeline);
                        currentPipeline = currentPipeline.UpstreamBuild?.Pipeline;
                    }
                    pipelines.Reverse();
                    for (int i = 0; i < pipelines.Count; i++)
                    {
                        var pipeline = pipelines[i];

                        if (i == 0)
                        {
                            if (pipeline.Project != null)
                            {
                                anchors.Add(new BuildStatsAnchor
                                {
                                    Name = pipeline.Project.Name,
                                    Url = "https://src.redpoint.games/" + pipeline.Project.PathWithNamespace,
                                });
                            }
                            if (pipeline.MergeRequest != null)
                            {
                                anchors.Add(new BuildStatsAnchor
                                {
                                    Name = pipeline.MergeRequest.Title,
                                    Url = pipeline.MergeRequest.Url,
                                    StartedUtcMillis = pipeline.CreatedAt?.ToUnixTimeMilliseconds(),
                                    EstimatedUtcMillis = (await _db.PipelineEstimations.FirstOrDefaultAsync(x => x.PipelineId == pipeline.Id))?.EstimatedFinishedAt?.ToUnixTimeMilliseconds(),
                                });
                            }
                            else
                            {
                                anchors.Add(new BuildStatsAnchor
                                {
                                    Name = !string.IsNullOrWhiteSpace(pipeline.Ref) ? pipeline.Ref : ("#" + pipeline.Id),
                                    Url = "https://src.redpoint.games/" + pipeline.Project?.PathWithNamespace + "/-/pipelines/" + pipeline.Id,
                                    StartedUtcMillis = pipeline.CreatedAt?.ToUnixTimeMilliseconds(),
                                    EstimatedUtcMillis = (await _db.PipelineEstimations.FirstOrDefaultAsync(x => x.PipelineId == pipeline.Id))?.EstimatedFinishedAt?.ToUnixTimeMilliseconds(),
                                });
                            }
                        }
                        else
                        {
                            anchors.Add(new BuildStatsAnchor
                            {
                                Name = pipeline.UpstreamBuild!.Stage,
                            });
                            anchors.Add(new BuildStatsAnchor
                            {
                                Name = pipeline.UpstreamBuild.Name,
                                Url = "https://src.redpoint.games/" + pipeline.Project?.PathWithNamespace + "/-/pipelines/" + pipeline.Id,
                                StartedUtcMillis = pipeline.CreatedAt?.ToUnixTimeMilliseconds(),
                                EstimatedUtcMillis = (await _db.PipelineEstimations.FirstOrDefaultAsync(x => x.PipelineId == pipeline.Id))?.EstimatedFinishedAt?.ToUnixTimeMilliseconds(),
                            });
                        }
                    }

                    anchors.Add(new BuildStatsAnchor
                    {
                        Name = build.Stage,
                    });
                    anchors.Add(new BuildStatsAnchor
                    {
                        Name = build.Name,
                        Url = "https://src.redpoint.games/" + build?.Pipeline?.Project?.PathWithNamespace + "/-/jobs/" + build!.Id,
                        StartedUtcMillis = build.StartedAt?.ToUnixTimeMilliseconds(),
                        EstimatedUtcMillis = (await _db.BuildEstimations.FirstOrDefaultAsync(x => x.BuildId == build.Id))?.EstimatedFinishedAt?.ToUnixTimeMilliseconds(),
                    });

                    stats.Builds.Add(new BuildStats
                    {
                        Id = build.Id,
                        Anchors = anchors,
                        Status = build.Status,
                    });
                }
                results.Add(stats);
            }

            return results;
        }

        private async Task<List<PipelineStats>> ReadPendingPipelineStats()
        {
            // Find top-level pending pipelines, then recursively find all of the
            // related downstream pipelines.
            var pipelinesToProcess = await _db.Pipelines
                .Include(x => x.Builds).ThenInclude(x => x.DownstreamPipeline)
                .Include(x => x.Builds).ThenInclude(x => x.Tests)
                .Include(x => x.MergeRequest)
                .Include(x => x.Project)
                .Where(x => x.Status != "success" && x.Status != "failed" && x.Status != "canceled" && x.Status != "manual" && x.Status != "skipped" && x.Status != null)
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

            var buildToUpdateWithEstimates = new List<PipelineBuildStats>();
            var buildToUpdateWithPipelineEstimates = new List<PipelineBuildStats>();
            var pipelinesToUpdateWithPipelineEstimates = new List<PipelineStats>();

            PipelineStats ConvertEntityToStats(PipelineEntity? pipeline, long? pipelineFallbackId)
            {
                if (pipeline == null && pipelineFallbackId != null)
                {
                    pipeline = childPipelineDictionary[pipelineFallbackId.Value];
                }

                ArgumentNullException.ThrowIfNull(pipeline);

                var stats = new PipelineStats();
                stats.Id = pipeline.Id;
                stats.Source = pipeline.Source;
                stats.StartedUtcMillis = pipeline.CreatedAt?.ToUnixTimeMilliseconds();
                pipelinesToUpdateWithPipelineEstimates.Add(stats);
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
                            buildToUpdateWithPipelineEstimates.Add(buildStats);
                        }
                        else if (build.Status == "pending")
                        {
                            buildToUpdateWithEstimates.Add(buildStats);

                            if (build.RunnerId == null)
                            {
                                buildStats.Status = "queued";
                            }
                        }
                        else
                        {
                            buildToUpdateWithEstimates.Add(buildStats);
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

            // Fetch estimates.
            var uniqueBuildIds = buildToUpdateWithEstimates.Select(x => x.Id).Distinct().ToList();
            var buildEstimates = await _db.BuildEstimations.Where(x => uniqueBuildIds.Contains(x.BuildId)).ToDictionaryAsync(k => k.BuildId, v => v);
            foreach (var build in buildToUpdateWithEstimates)
            {
                if (build?.Id != null && buildEstimates.TryGetValue(build.Id.Value, out var buildEstimate))
                {
                    build.EstimatedUtcMillis = buildEstimate?.EstimatedFinishedAt?.ToUnixTimeMilliseconds();
                }
            }
            var uniquePipelineIds = buildToUpdateWithPipelineEstimates.Select(x => x.DownstreamPipeline!.Id).Concat(pipelinesToUpdateWithPipelineEstimates.Select(x => x.Id)).Distinct().ToList();
            var pipelineEstimates = await _db.PipelineEstimations.Where(x => uniquePipelineIds.Contains(x.PipelineId)).ToDictionaryAsync(k => k.PipelineId, v => v);
            foreach (var build in buildToUpdateWithPipelineEstimates)
            {
                if (pipelineEstimates.TryGetValue(build.DownstreamPipeline!.Id, out var downstreamPipeline))
                {
                    build.EstimatedUtcMillis = downstreamPipeline?.EstimatedFinishedAt?.ToUnixTimeMilliseconds();
                }
            }
            foreach (var pipeline in pipelinesToUpdateWithPipelineEstimates)
            {
                if (pipelineEstimates.TryGetValue(pipeline.Id, out var pipelineToUpdate))
                {
                    pipeline.EstimatedUtcMillis = pipelineToUpdate?.EstimatedFinishedAt?.ToUnixTimeMilliseconds();
                }
            }

            // Return results.
            return results;
        }

        public async Task<DashboardStats> ReadAsync()
        {
            return new DashboardStats
            {
                PendingPipelineCount = await _db.Pipelines
                    .Include(x => x.Project)
                    .LongCountAsync(x =>
                        x.Status != "success" &&
                        x.Status != "failed" &&
                        x.Status != "canceled" &&
                        x.Status != "manual" &&
                        x.Status != "skipped" &&
                        x.Project != null
                    ),
                PendingBuildCount = await _db.Builds
                    .Include(x => x.Pipeline)
                    .Include(x => x.Pipeline!.Project)
                    .LongCountAsync(x =>
                        x.Status != "success" &&
                        x.Status != "failed" &&
                        x.Status != "canceled" &&
                        x.Status != "manual" &&
                        x.Status != "skipped" &&
                        x.Pipeline != null &&
                        x.Pipeline.Status != "success" &&
                        x.Pipeline.Status != "failed" &&
                        x.Pipeline.Status != "canceled" &&
                        x.Pipeline.Status != "manual" &&
                        x.Pipeline.Status != "skipped" &&
                        x.Pipeline.Project != null
                    ),
                Runners = (await ReadRunnerStats
                ()).OrderBy(x =>
                {
                    if (x.Description!.ToLowerInvariant().EndsWith("-windows", StringComparison.Ordinal))
                    {
                        return 0;
                    }
                    else if (x.Description.ToLowerInvariant().EndsWith("-mac", StringComparison.Ordinal))
                    {
                        return 1;
                    }
                    else if (x.Description.ToLowerInvariant().EndsWith("-linux", StringComparison.Ordinal))
                    {
                        return 2;
                    }
                    else if (x.Description.ToLowerInvariant().EndsWith("-docker", StringComparison.Ordinal))
                    {
                        return 3;
                    }
                    else
                    {
                        return 4;
                    }
                }).ThenBy(x => x.Description).ToList(),
                PendingPipelines = (await ReadPendingPipelineStats()).OrderByDescending(x => x.Id).ToList(),
            };
        }
    }
}