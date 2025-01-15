namespace Redpoint.CloudFramework.Processor
{
    using Microsoft.Extensions.DependencyInjection;
    using Quartz;
    using System.Threading.Tasks;

    internal class QuartzJobMapping<TProcessor> : IJob where TProcessor : IScheduledProcessor
    {
        private readonly IServiceProvider _serviceProvider;

        public QuartzJobMapping(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task Execute(IJobExecutionContext context)
        {
            var instance = _serviceProvider.GetRequiredService<TProcessor>();
            return instance.ExecuteAsync(context);
        }
    }
}
