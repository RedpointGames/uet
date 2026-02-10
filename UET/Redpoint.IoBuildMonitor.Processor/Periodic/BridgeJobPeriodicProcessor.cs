namespace Io.Processor.Periodic
{
    using Io.Database;
    using Io.Json.GitLab;
    using Io.Mappers;
    using Io.Redis;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.IoBuildMonitor.Mappers;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    public class BridgeJobPeriodicProcessor : PeriodicProcessor
    {
        private readonly ILogger<BridgeJobPeriodicProcessor> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public BridgeJobPeriodicProcessor(
            ILogger<PeriodicProcessor> baseLogger,
            ILogger<BridgeJobPeriodicProcessor> logger,
            IConfiguration configuration,
            IServiceScopeFactory serviceScopeFactory) : base(baseLogger)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceScopeFactory = serviceScopeFactory;
        }

        // TODO: This is currently a copy from WebhookEventProcessor. Move it to a DI interface.
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
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IoDbContext>();

                if (await dbContext.Pipelines.Where(x => x.Status != "success" && x.Status != "failed" && x.Status != "canceled" && x.Status != "manual" && x.Status != "skipped" && x.Status != null && x.UpstreamBuild == null && (x.Source == "pipeline" || x.Source == "parent_pipeline")).AnyAsync(cancellationToken: cancellationToken))
                {
                    // There are one or more pipelines that are downstream pipelines and they haven't been linked back to another pipeline yet. Rescan bridges.
                    foreach (var otherPipeline in await dbContext.Pipelines.Include(x => x.Project).Where(x => x.Status != "success" && x.Status != "failed" && x.Status != "canceled" && x.Status != "manual" && x.Status != "skipped" && x.Status != null && x.UpstreamBuild == null && x.Project != null).ToListAsync(cancellationToken: cancellationToken))
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

                    // Notify frontend.
                    await scope.ServiceProvider.GetRequiredService<INotificationHub>().NotifyAsync(NotificationType.DashboardUpdated);

                    // Save database changes.
                    await dbContext.SaveChangesAsync(cancellationToken);

                    // Check every 10 seconds.
                    await Task.Delay(10000, cancellationToken);
                }
                else
                {
                    // Check every minute.
                    await Task.Delay(60000, cancellationToken);
                }
            }
        }
    }
}
