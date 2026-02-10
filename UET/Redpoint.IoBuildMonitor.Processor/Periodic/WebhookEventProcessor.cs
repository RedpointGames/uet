namespace Io.Processor.Periodic
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
    using System.Text.Json.Serialization;
    using NodaTime;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Text.Json;
    using Redpoint.IoBuildMonitor.Mappers;

    public class WebhookEventProcessor : PeriodicProcessor
    {
        private readonly ILogger<WebhookEventProcessor> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public WebhookEventProcessor(
            ILogger<WebhookEventProcessor> logger,
            ILogger<PeriodicProcessor> baseLogger,
            IConfiguration configuration,
            IServiceScopeFactory serviceScopeFactory) : base(baseLogger)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceScopeFactory = serviceScopeFactory;
        }

        static Guid _processorReservationGuid = Guid.NewGuid();

        private async Task ResyncBridgeJobs(IServiceScope scope, long projectId, long pipelineId)
        {
            if (!string.IsNullOrWhiteSpace(_configuration["GitLab:AccessToken"]) &&
                !string.IsNullOrWhiteSpace(_configuration["GitLab:Domain"]))
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", _configuration["GitLab:AccessToken"]);

                    var bridgesString = await client.GetStringAsync(new Uri($"https://{_configuration["GitLab:Domain"]}/api/v4/projects/{projectId}/pipelines/{pipelineId}/bridges"));
                    var bridges = JsonSerializer.Deserialize(bridgesString, IoJsonSerializerContext.Default.BridgeJsonArray);

                    if (bridges != null)
                    {
                        var bridgeMapper = scope.ServiceProvider.GetRequiredService<IMapper<BridgeJson, BuildEntity>>();

                        foreach (var bridge in bridges)
                        {
                            await bridgeMapper.Map(bridge, new MapperContext());
                        }
                    }
                }
            }
        }

        protected override async Task ExecuteAsync(long iteration, CancellationToken cancellationToken)
        {
            if (iteration % 50 == 0)
            {
                using (var cleanupScope = _serviceScopeFactory.CreateScope())
                {
                    var cleanupDbContext = cleanupScope.ServiceProvider.GetRequiredService<IoDbContext>();

                    var threshold = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(-1));
                    foreach (var ev in cleanupDbContext.WebhookEvents.Where(x => x.Done == true && x.CreatedAt < threshold))
                    {
                        cleanupDbContext.WebhookEvents.Remove(ev);
                    }

                    await cleanupDbContext.SaveChangesAsync(cancellationToken);
                }
            }

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IoDbContext>();

                WebhookEventEntity? nextEvent = null;
                try
                {
                    using (var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken: cancellationToken))
                    {
                        nextEvent = dbContext.WebhookEvents
                            .OrderBy(x => x.CreatedAt)
                            .FirstOrDefault(x => x.Done == false && (x.ReservedBy == null || x.ReservedBy == _processorReservationGuid.ToString() || x.ReservationTimeout != null && x.ReservationTimeout < SystemClock.Instance.GetCurrentInstant()));
                        if (nextEvent != null)
                        {
                            nextEvent.ReservationTimeout = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromMinutes(1));
                            nextEvent.ReservedBy = _processorReservationGuid.ToString();
                            await dbContext.SaveChangesAsync(cancellationToken);
                            await transaction.CommitAsync(cancellationToken);
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    await Task.Delay(1000, cancellationToken);
                    return;
                }

                if (nextEvent == null)
                {
                    await Task.Delay(2000, cancellationToken);
                    return;
                }

                if (nextEvent.Data == null)
                {
                    // This event data is invalid, delete it.
                    _logger.LogWarning($"Deleted webhook event {nextEvent.Id} because it did not have any data.");
                    dbContext.WebhookEvents.Remove(nextEvent);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return;
                }

                try
                {
                    using (var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken: cancellationToken))
                    {
                        var notifyHistoryUpdated = false;
                        var notifyHealthUpdated = false;

                        if (nextEvent.ObjectKind == "pipeline")
                        {
                            PipelineWebhookJson? ev = null;
                            try
                            {
                                ev = JsonSerializer.Deserialize(
                                    nextEvent.Data,
                                    IoJsonSerializerContext.Default.PipelineWebhookJson);
                            }
                            catch
                            {
                            }

                            if (ev != null && ev.ObjectAttributes != null)
                            {
                                _logger.LogInformation($"Processing pipeline {ev.ObjectAttributes.Id}...");

                                var pipelineExists = await dbContext.Pipelines.AnyAsync(x => x.Id == ev.ObjectAttributes.Id, cancellationToken: cancellationToken);

                                var pipeline = await scope.ServiceProvider.GetRequiredService<IMapper<PipelineWebhookJson, PipelineEntity>>().Map(ev, new MapperContext
                                {
                                    WebhookEventId = nextEvent.Id,
                                    WebhookReceivedAt = nextEvent.CreatedAt ?? SystemClock.Instance.GetCurrentInstant(),
                                });

                                if (pipeline?.Status == "success" || pipeline?.Status == "failed" || pipeline?.Status == "canceled" || pipeline?.Status == "manual")
                                {
                                    notifyHistoryUpdated = true;
                                }

                                if (pipeline?.Ref == pipeline?.Project?.DefaultBranch)
                                {
                                    notifyHealthUpdated = true;
                                }

                                if (pipeline?.Project != null)
                                {
                                    try
                                    {
                                        await ResyncBridgeJobs(scope, pipeline.Project.Id, pipeline.Id);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex.Message, ex);
                                    }
                                }

                                if (!pipelineExists)
                                {
                                    // Resync bridge jobs for *all* pipelines we know of that are in-progress
                                    // in case there's an upstream pipeline we missed the bridge jobs for.
                                    await Task.Delay(1000, cancellationToken);
                                }
                            }
                        }
                        else if (nextEvent.ObjectKind == "build")
                        {
                            BuildWebhookJson? ev = null;
                            try
                            {
                                ev = JsonSerializer.Deserialize(
                                    nextEvent.Data,
                                    IoJsonSerializerContext.Default.BuildWebhookJson);
                            }
                            catch
                            {
                            }

                            if (ev != null)
                            {
                                _logger.LogInformation($"Processing build {ev.Id}...");

                                var build = await scope.ServiceProvider.GetRequiredService<IMapper<BuildWebhookJson, BuildEntity>>().Map(ev, new MapperContext
                                {
                                    WebhookEventId = nextEvent.Id,
                                    WebhookReceivedAt = nextEvent.CreatedAt ?? SystemClock.Instance.GetCurrentInstant(),
                                });

                                if (build != null)
                                {
                                    // Reprocess bridge jobs whenever a build changes status, because the bridge information is eventually consistent.
                                    // This is really less than ideal, but I'm not sure what we can do about it without changes to the GitLab API.
                                    foreach (var otherPipeline in await dbContext.Pipelines.Include(x => x.Project).Where(x => x.Status != "success" && x.Status != "failed" && x.Status != "canceled" && x.Status != "manual" && x.Status != "skipped" && x.Status != null && x.UpstreamBuild == null).ToListAsync(cancellationToken: cancellationToken))
                                    {
                                        if (otherPipeline.Project != null)
                                        {
                                            try
                                            {
                                                await ResyncBridgeJobs(scope, otherPipeline.Project.Id, otherPipeline.Id);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogError(ex.Message, ex);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Mark the webhook event entity as done.
                        nextEvent.ReservationTimeout = null;
                        nextEvent.ReservedBy = null;
                        nextEvent.Done = true;

                        await transaction.CommitAsync(cancellationToken);

                        await dbContext.SaveChangesAsync(cancellationToken);

                        _logger.LogInformation($"Successfully processed webhook event {nextEvent.Id}.");

                        await scope.ServiceProvider.GetRequiredService<INotificationHub>().NotifyAsync(NotificationType.DashboardUpdated);
                        if (notifyHistoryUpdated)
                        {
                            await scope.ServiceProvider.GetRequiredService<INotificationHub>().NotifyAsync(NotificationType.HistoryUpdated);
                        }
                        if (notifyHealthUpdated)
                        {
                            await scope.ServiceProvider.GetRequiredService<INotificationHub>().NotifyAsync(NotificationType.HealthUpdated);
                        }
                    }
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogInformation($"Unable to process webhook {nextEvent.Id} due to a concurrency exception. Moving back into queue.");

                    // We couldn't process this event. Unreserve it for processing later.
                    foreach (var entry in ex.Entries)
                    {
                        if (entry.State == EntityState.Added)
                        {
                            dbContext.Remove(entry.Entity);
                        }
                        else
                        {
                            await entry.ReloadAsync(cancellationToken);
                        }
                    }

                    // Unreserve the webhook entity.
                    nextEvent.ReservedBy = null;
                    nextEvent.ReservationTimeout = null;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}
