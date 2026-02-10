namespace Io.Processor
{
    using Io.Processor.Periodic;
    using Microsoft.Extensions.DependencyInjection;

    public static class ProcessorServiceExtensions
    {
        public static void AddProcessors(this IServiceCollection services)
        {
            services.AddHostedService<SyncStateOnStartup>();
            services.AddHostedService<WebhookEventProcessor>();
            services.AddHostedService<UtilizationDataProcessor>();
            services.AddHostedService<BridgeJobPeriodicProcessor>();
        }
    }
}
