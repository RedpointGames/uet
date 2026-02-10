namespace Io
{
    using Io.Database;
    using Io.Json.GitLab;
    using Io.Mappers;
    using Io.Redis;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NodaTime;
    using Redpoint.IoBuildMonitor.Mappers;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

    public class SyncStateOnStartup : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SyncStateOnStartup> _logger;

        public SyncStateOnStartup(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<SyncStateOnStartup> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        private class IncompleteBuild
        {
            public long ProjectId;
            public long BuildId;
            public BuildJson? FetchedBuildJson;
            public bool Missing;
        }

        private class IncompletePipeline
        {
            public long ProjectId;
            public long PipelineId;
            public PipelineJson? FetchedPipelineJson;
            public BridgeJson[] FetchedBridgesJson = [];
            public bool Missing;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var gitlabDomain = _configuration.GetValue<string>("GitLab:Domain");
                var accessToken = _configuration.GetValue<string>("GitLab:AccessToken");
                if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(gitlabDomain))
                {
                    _logger.LogWarning("Skipping state resync because GitLab access token is not set.");
                    return;
                }

                var incompleteBuilds = new List<IncompleteBuild>();
                var incompletePipelines = new List<IncompletePipeline>();

                var notifyHistoryUpdated = false;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IoDbContext>();

                    incompleteBuilds = await db.Builds
                        .Include(x => x.Pipeline)
                        .Include(x => x.Pipeline!.Project)
                        .Where(x => (x.Status != "success" && x.Status != "failed" && x.Status != "canceled" && x.Status != "manual" && x.Status != "skipped") || x.Runner != null)
                        .Where(x => x.Pipeline != null && x.Pipeline.Project != null).Select(x => new IncompleteBuild
                        {
                            BuildId = x.Id,
                            ProjectId = x.Pipeline!.Project!.Id,
                        })
                        .ToListAsync(cancellationToken: cancellationToken);

                    incompletePipelines = await db.Pipelines
                        .Include(x => x.Project)
                        .Where(x => x.Status != "success" && x.Status != "failed" && x.Status != "canceled" && x.Status != "manual" && x.Status != "skipped").Where(x => x.Project != null)
                        .Select(x => new IncompletePipeline
                        {
                            PipelineId = x.Id,
                            ProjectId = x.Project!.Id,
                        })
                        .ToListAsync(cancellationToken: cancellationToken);
                }

                await Task.WhenAll(
                    Parallel.ForEachAsync(incompleteBuilds, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (build, ct) =>
                    {
                        using (var client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", accessToken);

                            _logger.LogInformation($"Fetching latest information from GitLab: (build) {build.BuildId}");

                            var jobUrl = $"https://{_configuration["GitLab:Domain"]}/api/v4/projects/{build.ProjectId}/jobs/{build.BuildId}";
                            try
                            {
                                var jobString = await client.GetStringAsync(new Uri(jobUrl), ct);
                                build.FetchedBuildJson = JsonSerializer.Deserialize(jobString, IoJsonSerializerContext.Default.BuildJson);
                            }
                            catch (HttpRequestException rex) when (rex.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                build.Missing = true;
                            }
                        }
                    }),
                    Parallel.ForEachAsync(incompletePipelines, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (pipeline, ct) =>
                    {
                        using (var client = new HttpClient())
                        {
                            client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", accessToken);

                            _logger.LogInformation($"Fetching latest information from GitLab: (pipeline) {pipeline.PipelineId}");

                            var jobUrl = $"https://{_configuration["GitLab:Domain"]}/api/v4/projects/{pipeline.ProjectId}/pipelines/{pipeline.PipelineId}";
                            try
                            {
                                var pipelineString = await client.GetStringAsync(new Uri(jobUrl), ct);
                                pipeline.FetchedPipelineJson = JsonSerializer.Deserialize(
                                    pipelineString,
                                    IoJsonSerializerContext.Default.PipelineJson);

                                var bridgesString = await client.GetStringAsync(new Uri($"https://{_configuration["GitLab:Domain"]}/api/v4/projects/{pipeline.ProjectId}/pipelines/{pipeline.PipelineId}/bridges"), ct);
                                pipeline.FetchedBridgesJson = JsonSerializer.Deserialize(
                                    bridgesString,
                                    IoJsonSerializerContext.Default.BridgeJsonArray) ?? [];
                            }
                            catch (HttpRequestException rex) when (rex.StatusCode == System.Net.HttpStatusCode.NotFound)
                            {
                                pipeline.Missing = true;
                            }
                        }
                    }));

                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<IoDbContext>();
                    var bridgeMapper = scope.ServiceProvider.GetRequiredService<IMapper<BridgeJson, BuildEntity>>();

                    var buildIds = incompleteBuilds.Select(x => x.BuildId).ToHashSet();
                    var builds = await db.Builds.Where(x => buildIds.Contains(x.Id)).ToDictionaryAsync(k => k.Id, v => v, cancellationToken: cancellationToken);

                    var pipelineIds = incompletePipelines.Select(x => x.PipelineId).ToHashSet();
                    var pipelines = await db.Pipelines.Where(x => pipelineIds.Contains(x.Id)).ToDictionaryAsync(k => k.Id, v => v, cancellationToken: cancellationToken);

                    foreach (var incompleteBuildData in incompleteBuilds)
                    {
                        var incompleteBuild = builds[incompleteBuildData.BuildId];

                        if (!incompleteBuildData.Missing)
                        {
                            var job = incompleteBuildData.FetchedBuildJson;
                            if (job == null)
                            {
                                continue;
                            }

                            _logger.LogInformation($"Syncing pending build: {incompleteBuild.Id} (in status '{job.Status}')");

                            if (job.Runner != null &&
                                job.Status != "success" &&
                                job.Status != "failed" &&
                                job.Status != "canceled" &&
                                job.Status != "manual" &&
                                job.Status != "skipped")
                            {
                                incompleteBuild.Runner = await db.Runners.FirstOrDefaultAsync(x => x.Id == job.Runner.Id, cancellationToken: cancellationToken);
                                if (incompleteBuild.Runner == null)
                                {
                                    incompleteBuild.RunnerId = null;
                                }
                            }
                            else
                            {
                                incompleteBuild.Runner = null;
                                incompleteBuild.RunnerId = null;
                            }

                            incompleteBuild.Stage = job.Stage;
                            incompleteBuild.Name = job.Name;
                            incompleteBuild.Status = job.Status;
                            incompleteBuild.CreatedAt = job.CreatedAt != null ? Instant.FromDateTimeOffset(job.CreatedAt.Value) : null;
                            incompleteBuild.StartedAt = job.StartedAt != null ? Instant.FromDateTimeOffset(job.StartedAt.Value) : null;
                            incompleteBuild.FinishedAt = job.FinishedAt != null ? Instant.FromDateTimeOffset(job.FinishedAt.Value) : null;
                            incompleteBuild.Duration = job.Duration;
                        }
                        else
                        {
                            incompleteBuild.Runner = null;
                            incompleteBuild.RunnerId = null;
                            incompleteBuild.Status = "failed";
                            _logger.LogInformation($"Pending build was no longer found: {incompleteBuildData.BuildId}");
                        }
                    }

                    foreach (var incompletePipelineData in incompletePipelines)
                    {
                        var incompletePipeline = pipelines[incompletePipelineData.PipelineId];

                        if (!incompletePipelineData.Missing)
                        {
                            var pipeline = incompletePipelineData.FetchedPipelineJson;
                            if (pipeline == null)
                            {
                                continue;
                            }

                            _logger.LogInformation($"Syncing pending pipeline: {incompletePipeline.Id} (in status '{pipeline.Status}')");

                            incompletePipeline.Status = pipeline.Status;
                            incompletePipeline.CreatedAt = pipeline.CreatedAt != null ? Instant.FromDateTimeOffset(pipeline.CreatedAt.Value) : null;
                            incompletePipeline.FinishedAt = pipeline.FinishedAt != null ? Instant.FromDateTimeOffset(pipeline.FinishedAt.Value) : null;
                            incompletePipeline.Duration = pipeline.Duration;
                            incompletePipeline.QueuedDuration = pipeline.QueuedDuration;

                            if (incompletePipeline.Status == "success" || incompletePipeline.Status == "failed" || incompletePipeline.Status == "canceled")
                            {
                                notifyHistoryUpdated = true;
                            }

                            foreach (var bridge in incompletePipelineData.FetchedBridgesJson)
                            {
                                await bridgeMapper.Map(bridge, new MapperContext());
                            }
                        }
                        else
                        {
                            // This build has disappeared.
                            incompletePipeline.Status = "failed";
                            _logger.LogInformation($"Pending pipeline was no longer found: {incompletePipelineData.PipelineId}");
                        }
                    }

                    await db.SaveChangesAsync(cancellationToken);

                    await scope.ServiceProvider.GetRequiredService<INotificationHub>().NotifyAsync(NotificationType.DashboardUpdated);
                    if (notifyHistoryUpdated)
                    {
                        await scope.ServiceProvider.GetRequiredService<INotificationHub>().NotifyAsync(NotificationType.HistoryUpdated);
                    }
                }

                _logger.LogInformation($"Pending job/pipeline synchronisation finished.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
